# Alpha Runtime State Envelope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a face classifiable `ProvenOpaque` only when it is proven opaque in every admitted runtime state, by observing the committed controller graph, conservatively enumerating a finite set of admitted material states, and re-running the existing alpha proof pipeline across all of them.

**Architecture:** A capture pass declared with NDMF's real `WithRequiredExtension` mechanism retains `IPlatformAnimatorBindings`; the barrier pass declares no animator extension, so NDMF commits every controller before AMUSE reads it. AMUSE walks the committed real controller graph into a *transient* live observation, closes material-swap dependencies, converts everything to immutable evidence and discards the transient observation, admits only finite-exact singleton property values and exactly enumerable material swaps, and intersects `ProvenOpaque` per triangle using `AlphaSemanticsResolver` and `TriangleAlphaClassifier` unchanged.

**Tech Stack:** Unity 2022.3.22f1, C# Editor-only code, NDMF 1.14.4 public APIs (`BuildContext`, `Sequence.WithRequiredExtension`, `AnimatorServicesContext`, `IPlatformAnimatorBindings`, `IVirtualizeAnimatorController`, `IVirtualizeMotion`), `UnityEditor.Animations`, `AnimationUtility`, `UnityEditor.PackageManager.PackageInfo`, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency, assembly, asmdef reference, runtime component, or reflection.

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
- **The live-to-immutable boundary is one-way and closes inside host capture.** Transient host observations may hold live Unity references; nothing downstream of Task 10 may. Proof, admitted-state construction, and classification never call back into `AnimatorController`, `AnimationClip`, `AnimationCurve`, `Material`, `Renderer`, or `Mesh`.
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
| `Packages/com.alrauna.amuse/Editor/Build/AmuseAnimatorBindingsCapture.cs` | Create | Extension-declaring capture pass; retains `IPlatformAnimatorBindings` and any state Task 6 requires. |
| `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` | Modify | `WithRequiredExtension` capture pass, then the extension-free barrier pass. |
| `Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs` | Create | Enumerate committed controllers; avatar-scoped refusals. |
| `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs` | Create | **Transient** live host observation. Holds live references; never escapes host capture. |
| `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs` | Create | Immutable animation evidence value types. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs` | Create | The eager capture pipeline and the one-way live-to-immutable conversion. |
| `Packages/com.alrauna.amuse/Editor/Host/BehaviourIdentity.cs` | Create | Authorization-grade behaviour identity and the version-pinned allowlist. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs` | Modify | Presence-preserving substitution derivations. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` | Modify | Admitted states, structural refusals, per-triangle intersection. |
| `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` | Create | Singleton rule, colour/vector reconstruction, budget, dedup. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs` | Create | Architecture gate and `GetInnateControllers` safety. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs` | Create | Obligations 1–4 and 8 recorded as observed behavior. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs` | Create | Enumeration and avatar-scoped refusals. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs` | Create | Capture, closure ordering, one-way boundary. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/BehaviourIdentityTests.cs` | Create | Identity, allowlist, spoof resistance. |
| `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` | Create | Scalar, colour, vector, budget, dedup. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs` | Modify | Recursive no-live-Unity-object guard. |

---

## Task 1: ARCHITECTURE GATE — `IPlatformAnimatorBindings` lifetime

Verification obligation 6 and the **first architecture gate**. The whole two-pass observation route depends on it.

**If this task's verification fails, STOP. Do not proceed to Task 2. Do not substitute a mock, a fixture workaround, or a re-activated context. Report the failure as an architectural blocker and return to design.**

The gate is only valid if the capture pass declares the extension through **the same real NDMF mechanism production will use**: `Sequence.WithRequiredExtension` ([Editor/API/Fluent/Sequence/Extensions.cs:153](Packages/nadena.dev.ndmf/Editor/API/Fluent/Sequence/Extensions.cs:153)), which feeds `SolverPass.RequiredExtensions` and therefore the resolver's activate/deactivate plan. Calling `context.Extension<T>()` without declaring is not a declaration and would prove nothing.

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the proven fact that a captured `IPlatformAnimatorBindings` survives `AnimatorServicesContext` deactivation.

- [ ] **Step 1: Write the failing gate test**

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
            internal bool ContextActiveAtCapture;
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
                var sequence = InPhase(BuildPhase.PlatformFinish);

                // The real production declaration mechanism. This is what puts
                // AnimatorServicesContext into SolverPass.RequiredExtensions and
                // makes the resolver activate it before, and deactivate it after.
                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run("capture bindings", Capture));

                // Declared outside the scope: no animator extension required, so
                // the resolver deactivates and commits before this pass.
                sequence.Run("observe after commit", Observe);
            }

            private static void Capture(BuildContext context)
            {
                if (!SyntheticPluginScope.IsArmed) return;
                var probe = context.GetState<GateProbe>();
                var services = context.Extension<AnimatorServicesContext>();
                probe.ContextActiveAtCapture = true;
                probe.Captured = services.ControllerContext.PlatformBindings;
                probe.CaptureRan = true;
            }

            private static void Observe(BuildContext context)
            {
                if (!SyntheticPluginScope.IsArmed) return;
                var probe = context.GetState<GateProbe>();
                probe.ObserveRan = true;

                // Non-perturbing inactivity check. BuildContext.Extension<T> only
                // reads _activeExtensions and throws when absent; it never
                // activates (Editor/API/BuildContext.cs:105-112). Catching that
                // throw therefore observes without changing lifecycle state.
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
                Assert.That(probe.ContextActiveAtCapture, Is.True,
                    "WithRequiredExtension did not activate AnimatorServicesContext");
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

Reuse the existing `SyntheticPluginScope`, `OverrideTemporaryDirectoryScope`, and `TestVrchatPlatform` helpers from `AmusePlatformFinishPluginTests.cs`. If `SyntheticPluginScope` does not expose `IsArmed`, add that one internal property rather than duplicating the scope type.

- [ ] **Step 2: Run the gate test**

Run the EditMode suite filtered to `AnimatorBindingsLifetimeGateTests`, on the public development project only, after confirming `Application.dataPath == <repo-root>/Assets`.

Expected on first run: FAIL (type and helper visibility do not exist yet).

- [ ] **Step 3: Make the test compile and run**

Add only the `IsArmed` property if missing. No production code.

- [ ] **Step 4: Run the gate test and record the observed result**

Expected: PASS.

**If any gate assertion FAILS, STOP HERE.** Record which assertion and the exact exception. Do not adapt the test. Do not re-activate the context in the observe pass — that restores the marker blind spot the spec exists to remove. Report as an architectural blocker; the design must be revised.

- [ ] **Step 5: Record the outcome in the spec's obligation 6**

Append one sentence stating it was verified, the date, and the test. Do not restructure the spec.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: prove animator bindings survive context deactivation"
```

---

## Task 2: Characterize float binding names, including colour and vector components

Verification obligation 2. Establishes the exact binding forms Tasks 11–13 depend on, for **scalar, colour, and vector** properties.

**Files:**
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the recorded `EditorCurveBinding.propertyName` forms for per-slot scalar, colour-component, and vector-component animation.

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
        private static string[] RoundTripFloatBindings(params string[] properties)
        {
            var clip = new AnimationClip { name = "characterization" };
            try
            {
                foreach (var property in properties)
                {
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(
                            "", typeof(SkinnedMeshRenderer), property),
                        AnimationCurve.Constant(0f, 1f, 0.5f));
                }

                return AnimationUtility.GetCurveBindings(clip)
                    .Select(b => b.propertyName)
                    .OrderBy(n => n, System.StringComparer.Ordinal)
                    .ToArray();
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ScalarAndSlotBindingNamesAreRecorded()
        {
            var bindings = RoundTripFloatBindings(
                "material._Cutoff", "material[1]._Cutoff");

            // Records exactly what Unity round-trips. If this fails, change the
            // CODE and the spec's obligation 2 -- not the expectation -- because
            // the slot mapping rule in Task 11 would then be wrong.
            Assert.That(bindings, Is.EqualTo(new[]
            {
                "material._Cutoff",
                "material[1]._Cutoff",
            }));
        }

        [Test]
        public void ColourComponentBindingNamesAreRecorded()
        {
            var bindings = RoundTripFloatBindings(
                "material._Color.r", "material._Color.g",
                "material._Color.b", "material._Color.a");

            Assert.That(bindings, Is.EqualTo(new[]
            {
                "material._Color.a",
                "material._Color.b",
                "material._Color.g",
                "material._Color.r",
            }));
        }

        [Test]
        public void VectorComponentBindingNamesAreRecorded()
        {
            var bindings = RoundTripFloatBindings(
                "material._MainTex_ST.x", "material._MainTex_ST.y",
                "material._MainTex_ST.z", "material._MainTex_ST.w");

            Assert.That(bindings, Is.EqualTo(new[]
            {
                "material._MainTex_ST.w",
                "material._MainTex_ST.x",
                "material._MainTex_ST.y",
                "material._MainTex_ST.z",
            }));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — file does not exist.

- [ ] **Step 3: Run and reconcile with observed reality**

No production code in this task. Run the tests and adjust the **expected** arrays to whatever Unity actually produced. This is the one place in the plan where matching the test to observed behavior is correct, because recording reality is the test's purpose. If a component suffix differs from `.r/.g/.b/.a` or `.x/.y/.z/.w`, record the real form — Task 11 parses exactly what is recorded here.

- [ ] **Step 4: Run and verify all three pass**

Expected: PASS.

- [ ] **Step 5: Update the spec's obligation 2 with the recorded forms**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: characterize scalar, colour, and vector binding names"
```

---

## Task 3: Characterize object-reference and array-size binding categories

Verification obligation 3, plus the category question Task 15 depends on. **Ends as an exact executable specification — no disjunctive assertion survives this task.**

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: recorded facts — whether texture-reference object curves on materials exist, and which curve category carries `m_Materials.Array.size`.

- [ ] **Step 1: Write the failing exploratory test**

```csharp
        [Test]
        public void ObjectReferenceAndArraySizeCategoriesAreRecorded()
        {
            var clip = new AnimationClip { name = "category characterization" };
            var texture = new Texture2D(1, 1);
            var material = new Material(Shader.Find("Unlit/Color"));
            try
            {
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "", typeof(SkinnedMeshRenderer), "material._MainTex"),
                    new[] { new ObjectReferenceKeyframe { time = 0f, value = texture } });

                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "", typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[0]"),
                    new[] { new ObjectReferenceKeyframe { time = 0f, value = material } });

                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        "", typeof(SkinnedMeshRenderer), "m_Materials.Array.size"),
                    AnimationCurve.Constant(0f, 1f, 2f));

                var floats = AnimationUtility.GetCurveBindings(clip)
                    .Select(b => b.propertyName).OrderBy(n => n).ToArray();
                var objects = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .Select(b => b.propertyName).OrderBy(n => n).ToArray();

                TestContext.WriteLine("float bindings: " + string.Join(", ", floats));
                TestContext.WriteLine("object bindings: " + string.Join(", ", objects));

                // Temporary during Step 3 only. Step 5 replaces this with exact
                // expected arrays. This assertion must NOT survive the task.
                Assert.That(floats.Length + objects.Length, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Run and read the recorded output**

Note precisely: whether `material._MainTex` survives as an object binding, and whether `m_Materials.Array.size` lands in the float list or the object list.

- [ ] **Step 4: Rewrite the test as an exact specification**

Replace the whole method with two exact assertions using the observed values, for example:

```csharp
                Assert.That(floats, Is.EqualTo(new[] { "m_Materials.Array.size" }));
                Assert.That(objects, Is.EqualTo(new[]
                {
                    "m_Materials.Array.data[0]",
                }));
```

using whatever was actually observed. **The `Is.GreaterThan(0)` assertion must be gone.** Add a comment naming this the executable specification for obligation 3 and for the slot-count category.

- [ ] **Step 5: Run and verify it passes**

Expected: PASS.

- [ ] **Step 6: Update the spec's obligation 3**

Record whether texture-reference object curves exist. If they do, add a note that texture assignment is an admitted-state dimension and raise it with the user before Task 10 — the spec's admitted-state construction does not currently cover it, and adding it is a design change, not a plan change. If they do not, record that it is structurally impossible.

- [ ] **Step 7: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: specify object reference and array size binding categories"
```

---

## Task 4: Characterize curve interpolation bounds

Verification obligation 8.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`

**Interfaces:**
- Consumes: nothing. Produces: a recorded fact only.

- [ ] **Step 1: Write the failing tests**

```csharp
        [Test]
        public void InterpolatingCurveProducesIntermediateValues()
        {
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            Assert.That(curve.Evaluate(0.5f), Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void ConstantTangentCurveTakesOnlyKeyframeValues()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f) { outTangent = float.PositiveInfinity },
                new Keyframe(1f, 1f) { inTangent = float.PositiveInfinity });

            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(0f).Or.EqualTo(1f));
        }
```

- [ ] **Step 2: Run to verify they fail**

Expected: FAIL — methods not present.

- [ ] **Step 3: Add the tests**

No production code.

- [ ] **Step 4: Run and verify they pass**

Expected: PASS. If `ConstantTangentCurveTakesOnlyKeyframeValues` fails, the stepped-curve admission rule is wrong — STOP and report, because Task 12 depends on it.

- [ ] **Step 5: Update the spec's obligation 8**

Record within-curve interpolation. Note that cross-source blending still requires a Play Mode observation and remains open, so the conservative singleton rule stands.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: characterize curve interpolation bounds"
```

---

## Task 5: Characterize animation of a property absent from a material

Determines the presence semantics Task 17's substitution must preserve. Without this, substitution would invent a material property that does not exist.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the recorded rule for "animation binds `material._X`, admitted material has no `_X`".

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void AbsentPropertyAssignmentSemanticsAreRecorded()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            try
            {
                Assert.That(material.HasProperty("_NotInThisShader"), Is.False);

                material.SetFloat("_NotInThisShader", 0.25f);

                // Records whether assigning an absent property makes it present.
                // Task 17 must preserve whatever is observed here; it must never
                // set HasValue merely because a substitution occurred.
                TestContext.WriteLine(
                    "absent property became present: " +
                    material.HasProperty("_NotInThisShader"));
                Assert.That(material.HasProperty("_NotInThisShader"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Run and reconcile with observed reality**

If `HasProperty` remains false, the recorded rule is **absent stays absent, and the animated binding is ineffective on that material**; keep the assertion as written. If it becomes true, invert the assertion to record that, and Task 17 must instead refuse — see that task's branch.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS with whichever behavior was observed.

- [ ] **Step 5: Record the rule in the spec's obligation 4 section as a new sub-note**

State the observed behavior and which Task 17 branch it selects. Do not restructure the spec.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: characterize animation of an absent material property"
```

---

## Task 6: Characterize `GetInnateControllers` side-effect safety

Verification obligation 1. **Equal counts are not evidence.** This task compares observable state before and after repeated invocation.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs`

**Interfaces:**
- Consumes: the retained bindings from Task 1.
- Produces: the decision of where `GetInnateControllers` may be called, which Task 7 obeys.

- [ ] **Step 1: Write the failing test**

Extend `GateProbe`:

```csharp
            internal string FirstEnumeration;
            internal string SecondEnumeration;
            internal string AnimatorAssignmentsBefore;
            internal string AnimatorAssignmentsAfter;
            internal bool EnumerationStable;
            internal bool ObservableStateUnchanged;
```

Add a helper in the test class that renders comparable state as a string — controller **identities**, not counts:

```csharp
        private static string DescribeEnumeration(
            IEnumerable<(object, RuntimeAnimatorController, bool)> innate)
        {
            return string.Join("|", innate.Select(entry =>
                entry.Item1 + "=>" +
                (entry.Item2 == null
                    ? "null"
                    : entry.Item2.GetInstanceID().ToString()) +
                ":" + entry.Item3));
        }

        private static string DescribeAnimatorAssignments(GameObject root)
        {
            return string.Join("|", root.GetComponentsInChildren<Animator>(true)
                .Select(a => a.GetInstanceID() + "=>" +
                    (a.runtimeAnimatorController == null
                        ? "null"
                        : a.runtimeAnimatorController.GetInstanceID().ToString())));
        }
```

In `Observe`, capture assignments, enumerate, enumerate again, capture assignments again:

```csharp
                    probe.AnimatorAssignmentsBefore =
                        DescribeAnimatorAssignments(context.AvatarRootObject);
                    probe.FirstEnumeration = DescribeEnumeration(
                        probe.Captured.GetInnateControllers(context.AvatarRootObject));
                    probe.SecondEnumeration = DescribeEnumeration(
                        probe.Captured.GetInnateControllers(context.AvatarRootObject));
                    probe.AnimatorAssignmentsAfter =
                        DescribeAnimatorAssignments(context.AvatarRootObject);
                    probe.EnumerationStable = string.Equals(
                        probe.FirstEnumeration, probe.SecondEnumeration,
                        StringComparison.Ordinal);
                    probe.ObservableStateUnchanged = string.Equals(
                        probe.AnimatorAssignmentsBefore,
                        probe.AnimatorAssignmentsAfter,
                        StringComparison.Ordinal);
```

And the test:

```csharp
        [Test]
        public void RepeatedInnateEnumerationIsIdempotentAndSideEffectFree()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE innate enumeration safety");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var probe = context.GetState<GateProbe>();

                Assert.That(probe.Failure, Is.Null);
                Assert.That(probe.EnumerationStable, Is.True,
                    "GetInnateControllers returned different controller identities " +
                    "on a repeated call: " + probe.FirstEnumeration + " vs " +
                    probe.SecondEnumeration);
                Assert.That(probe.ObservableStateUnchanged, Is.True,
                    "GetInnateControllers changed animator controller assignments: " +
                    probe.AnimatorAssignmentsBefore + " vs " +
                    probe.AnimatorAssignmentsAfter);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — new probe fields are not defined.

- [ ] **Step 3: Add the probe fields, helpers, and second call**

- [ ] **Step 4: Run and record the outcome**

Expected: PASS.

**If either assertion fails, take the spec's conservative alternate path** stated in obligation 1: the barrier pass must not call `GetInnateControllers`. Instead the Task 1 capture pass records the innate `(key, controller)` pairs into `AmusePlatformFinishState` while the context is active, and Task 7 consumes those recorded pairs, re-resolving each key's committed controller from the avatar after commit. Do not assume stability; follow whichever branch is observed and note it in the spec.

Note the scope limit honestly in the spec: this fixture has no VRChat avatar descriptor, so it exercises the generic bindings path. Record that the descriptor-specific side effects noted in obligation 1 (`customizeAnimationLayers`, descriptor editor instantiation) remain unverified here and are covered only by the conservative path.

- [ ] **Step 5: Update the spec's obligation 1**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: characterize innate controller enumeration safety"
```

---

## Task 7: Committed-controller enumeration and unsupported-form refusal

Verification obligation 5.

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs`

**Interfaces:**
- Consumes: `IPlatformAnimatorBindings` from Task 1, under the branch chosen in Task 6.
- Produces:
  - `internal enum AvatarAnimationRefusal { None, UnsupportedAnimatorControllerForm, UnsupportedSyncedLayerOverrides, UnrecognizedStateMachineBehaviour }`
  - `internal sealed class CommittedLayer { internal string ControllerName { get; } internal int LayerIndex { get; } internal AnimatorLayerBlendingMode BlendingMode { get; } internal IReadOnlyList<AnimationClip> Clips { get; } internal IReadOnlyList<StateMachineBehaviour> Behaviours { get; } internal bool HasUnnormalizedDirectBlendTree { get; } }`
  - `internal sealed class CommittedControllerGraphResult { internal AvatarAnimationRefusal Refusal { get; } internal IReadOnlyList<CommittedLayer> Layers { get; } }`
  - `internal static CommittedControllerGraphResult CommittedControllerGraph.Enumerate(GameObject avatarRoot, IPlatformAnimatorBindings bindings)`

`CommittedLayer` deliberately holds live `AnimationClip` and `StateMachineBehaviour` references. It is **transient host enumeration**, consumed only by Task 8, never by proof. The Task 20 guard applies to captured evidence, not to this type.

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
                    root, new StubBindings(over));

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
                controller.layers[0].stateMachine.AddState("S0").motion = clip;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

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

Write `StubBindings` in this test file as a minimal `IPlatformAnimatorBindings` returning one `(key, controller, false)` tuple with `IsSpecialMotion => false`. This is a double for the *enumeration input*, not for the Task 1 gate, and is permitted.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL with "CommittedControllerGraph does not exist".

- [ ] **Step 3: Implement enumeration**

Walk the innate controllers, plus `GetComponentsInChildren<IVirtualizeAnimatorController>(true)` and `GetComponentsInChildren<IVirtualizeMotion>(true)`. Any `RuntimeAnimatorController` that is not an `AnimatorController` returns `UnsupportedAnimatorControllerForm`. Any layer with `syncedLayerIndex >= 0` returns `UnsupportedSyncedLayerOverrides`. Otherwise collect every reachable `AnimationClip` through state machines, child state machines, states, and blend trees, and collect state and state-machine behaviours. Set `HasUnnormalizedDirectBlendTree` when a reachable `BlendTree` has `blendType == BlendTreeType.Direct` with normalization off; read normalization through the serialized `m_NormalizedBlendValues` property if no public accessor exists in this Unity version, and re-check for a public accessor first. Deduplicate clips by reference. A refusal returns empty `Layers` so no partial result can be consumed.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Update the spec's obligation 5**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "feat: enumerate the committed controller graph"
```

---

## Task 8: Transient live animation observation

The first half of the eager-capture boundary. This type is **allowed** to hold live object-reference keyframe values, because admitted materials do not exist yet. It is discarded in Task 10.

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `CommittedControllerGraphResult` from Task 7.
- Produces:
  - `internal sealed class LiveFloatObservation { internal string Path { get; } internal string TypeName { get; } internal string PropertyName { get; } internal bool IsFiniteExact { get; } internal IReadOnlyList<float> Values { get; } }`
  - `internal sealed class LiveObjectObservation { internal string Path { get; } internal string TypeName { get; } internal string PropertyName { get; } internal IReadOnlyList<Object> Values { get; } }`
  - `internal sealed class LiveClipObservation { internal string Name { get; } internal bool IsSpecialMotion { get; } internal IReadOnlyList<LiveFloatObservation> Floats { get; } internal IReadOnlyList<LiveObjectObservation> Objects { get; } }`
  - `internal static LiveClipObservation LiveAnimationObservation.ObserveClip(AnimationClip clip, bool isSpecialMotion)`

The file carries a header comment: *this type intentionally holds live `UnityEngine.Object` references, exists only inside host capture, and must never be referenced from `Analysis` or from any captured-evidence type.*

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
        public void FloatObservationCopiesKeyframeValues()
        {
            var clip = new AnimationClip { name = "observed" };
            var binding = EditorCurveBinding.FloatCurve(
                "Body", typeof(SkinnedMeshRenderer), "material._Cutoff");
            AnimationUtility.SetEditorCurve(
                clip, binding, AnimationCurve.Constant(0f, 1f, 0.25f));

            try
            {
                var observed = LiveAnimationObservation.ObserveClip(clip, false);

                AnimationUtility.SetEditorCurve(
                    clip, binding, AnimationCurve.Constant(0f, 1f, 0.75f));

                Assert.That(observed.Floats.Single().Values.Single(),
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
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    "Body", typeof(SkinnedMeshRenderer), "material._Cutoff"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));

            try
            {
                var observed = LiveAnimationObservation.ObserveClip(clip, false);

                Assert.That(observed.Floats.Single().IsFiniteExact, Is.False);
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

Expected: FAIL — `LiveAnimationObservation` does not exist.

- [ ] **Step 3: Implement observation**

Read `AnimationUtility.GetCurveBindings` and `GetObjectReferenceCurveBindings`, copying every keyframe value. `IsFiniteExact` is true only when every segment is constant: a single key; or every consecutive key pair where `outTangent` and the next `inTangent` are both `float.PositiveInfinity`, or both keys carry the same value. Any `weightedMode` other than `WeightedMode.None` sets it false. Wrap collections in `ReadOnlyCollection<T>`.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: observe committed clips into a transient live form"
```

---

## Task 9: Structural material-swap discovery

Operates on the transient observation from Task 8.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `LiveObjectObservation` from Task 8.
- Produces: `internal static bool LiveAnimationObservation.TryParseMaterialSlotBinding(string propertyName, out int slotIndex)`

- [ ] **Step 1: Write the failing test**

```csharp
        [TestCase("m_Materials.Array.data[0]", 0)]
        [TestCase("m_Materials.Array.data[3]", 3)]
        public void MaterialSlotBindingsAreParsed(string property, int expected)
        {
            Assert.That(LiveAnimationObservation.TryParseMaterialSlotBinding(
                property, out var slot), Is.True);
            Assert.That(slot, Is.EqualTo(expected));
        }

        [TestCase("m_Materials.Array.size")]
        [TestCase("m_Mesh")]
        [TestCase("material._Cutoff")]
        [TestCase("m_Materials.Array.data[]")]
        [TestCase("m_Materials.Array.data[-1]")]
        public void NonSlotBindingsAreNotParsedAsSlots(string property)
        {
            Assert.That(LiveAnimationObservation.TryParseMaterialSlotBinding(
                property, out _), Is.False);
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement parsing**

Exact ordinal prefix `m_Materials.Array.data[`, exact suffix `]`, and an invariant non-negative integer between them. Reject everything else, including `m_Materials.Array.size` and an empty or negative index. Do not use a regular expression.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: discover animated material slot assignments"
```

---

## Task 10: Dependency closure and the one-way live-to-immutable conversion

**This task closes the eager-capture boundary and carries the ordering the spec makes normative.** Its central test must fail if relevance is derived from the initially assigned material's family.

The full pipeline, all inside one eager host-capture phase:

```
committed controllers
  -> transient live curve/object observations        (Tasks 8-9)
  -> discover material swaps                          (Task 9)
  -> close admitted material families/requests        (this task)
  -> capture all admitted materials immutably         (this task)
  -> replace live references with immutable indices   (this task)
  -> discard transient live observations              (this task)
```

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `LiveClipObservation` and `TryParseMaterialSlotBinding`; `UnityMaterialSemantics.CaptureAlphaMaterials`; `MaterialEvidenceRequest.Combine`.
- Produces:
  - `internal sealed class CapturedFloatBinding { internal string Path { get; } internal string TypeName { get; } internal string PropertyName { get; } internal bool IsFiniteExact { get; } internal IReadOnlyList<float> Values { get; } }`
  - `internal sealed class CapturedObjectBinding { internal string Path { get; } internal string TypeName { get; } internal string PropertyName { get; } internal IReadOnlyList<int> AdmittedMaterialIndices { get; } }`
  - `internal sealed class CapturedClipEvidence { internal string Name { get; } internal bool IsSpecialMotion { get; } internal IReadOnlyList<CapturedFloatBinding> FloatBindings { get; } internal IReadOnlyList<CapturedObjectBinding> ObjectBindings { get; } }`
  - `internal sealed class CapturedAnimationEvidence { internal bool IsClosed { get; } internal MaterialEvidenceRequest RelevanceRequest { get; } internal IReadOnlyList<CapturedClipEvidence> Clips { get; } internal IReadOnlyList<CapturedAlphaMaterial> AdmittedMaterials { get; } internal IReadOnlyList<string> BehaviourIdentities { get; } internal bool HasUnnormalizedDirectBlendTree { get; } internal bool HasAdditiveLayer { get; } }`
  - `internal static CapturedAnimationEvidence UnityAnimationEvidenceCapture.Capture(IReadOnlyList<LiveClipObservation> observations, IReadOnlyList<Material> currentSlots, CommittedControllerGraphResult graph)`

`AdmittedMaterialIndices` indexes into `AdmittedMaterials`, which is populated **before** any `CapturedObjectBinding` is constructed. `IsSpecialMotion` is diagnostic only and must never gate a decision.

- [ ] **Step 1: Write the failing closure-ordering test**

The decisive case: slot 0 currently holds a material of family A; animation can assign a material of family B. The relevance request must contain properties that **only** B requests.

```csharp
        [Test]
        public void ClosureUnionsEveryAdmittedFamilyNotOnlyTheInitialOne()
        {
            // Build both materials through the existing attested fixture helpers so
            // both resolve through real frontends. A stock Unity shader resolves
            // all-Unknown and would make this test pass vacuously.
            var initial = PoiyomiFixtureTestBase.CreateAttestedMaterial();
            var swapped = LilToonFixtureTestBase.CreateAttestedMaterial();
            try
            {
                var swapObservation = ObservationWithMaterialSwap(swapped);

                var evidence = UnityAnimationEvidenceCapture.Capture(
                    new[] { swapObservation },
                    new[] { initial },
                    EmptyGraph());

                Assert.That(evidence.IsClosed, Is.True);
                Assert.That(evidence.AdmittedMaterials.Count, Is.EqualTo(2));

                // Derive the expectation from the frontends themselves so the test
                // cannot drift from them.
                var onlyInSwapped = LilToonMaterialSemantics
                    .AlphaEvidenceRequest.ScalarProperties
                    .Except(PoiyomiMaterialSemantics
                        .AlphaEvidenceRequest.ScalarProperties)
                    .ToArray();
                Assert.That(onlyInSwapped, Is.Not.Empty,
                    "fixture precondition: the two families must differ");

                foreach (var name in onlyInSwapped)
                {
                    Assert.That(evidence.RelevanceRequest.ScalarProperties,
                        Contains.Item(name),
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
        public void ClosureFailsAndYieldsAnEmptyRelevanceRequest()
        {
            var initial = PoiyomiFixtureTestBase.CreateAttestedMaterial();
            try
            {
                var evidence = UnityAnimationEvidenceCapture.Capture(
                    new[] { ObservationWithUnresolvableSwap() },
                    new[] { initial },
                    EmptyGraph());

                Assert.That(evidence.IsClosed, Is.False);
                Assert.That(evidence.RelevanceRequest.ScalarProperties, Is.Empty);
                Assert.That(evidence.RelevanceRequest.TextureProperties, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(initial);
            }
        }

        [Test]
        public void CapturedEvidenceHoldsNoLiveMaterialReference()
        {
            var initial = PoiyomiFixtureTestBase.CreateAttestedMaterial();
            var swapped = LilToonFixtureTestBase.CreateAttestedMaterial();
            try
            {
                var evidence = UnityAnimationEvidenceCapture.Capture(
                    new[] { ObservationWithMaterialSwap(swapped) },
                    new[] { initial },
                    EmptyGraph());

                var binding = evidence.Clips.Single().ObjectBindings.Single();

                // Indices, never references.
                Assert.That(binding.AdmittedMaterialIndices,
                    Is.All.InRange(0, evidence.AdmittedMaterials.Count - 1));

                // Destroying every live material must not affect the evidence.
                Object.DestroyImmediate(swapped);
                Assert.That(evidence.AdmittedMaterials[
                    binding.AdmittedMaterialIndices[0]], Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(initial);
            }
        }
```

Write `ObservationWithMaterialSwap`, `ObservationWithUnresolvableSwap`, and `EmptyGraph` as private helpers in this test file. If the attested fixture helpers do not currently expose a `CreateAttestedMaterial` entry point, add the smallest internal helper to `PoiyomiFixtureTestBase` and `LilToonFixtureTestBase` rather than duplicating attestation setup.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `UnityAnimationEvidenceCapture` does not exist.

- [ ] **Step 3: Implement the pipeline in the normative order**

Each step completes before the next begins:

1. from the transient observations, collect every slot binding and its live keyframe `Material` values;
2. enumerate admitted materials per slot as the current assignment plus every keyframe value;
3. attest each admitted material's family through `UnityMaterialSemantics`;
4. `MaterialEvidenceRequest.Combine` the alpha requests of every attested family;
5. capture every admitted material's evidence through the closed union;
6. build `CapturedObjectBinding` values holding **indices** into the captured list;
7. return; the transient observations go out of scope and are never stored.

`IsClosed` is false when any slot's admitted set cannot be fully enumerated, or when any admitted material's family cannot be attested. When false, `RelevanceRequest` is the empty request, `Clips` is empty, and the caller refuses. A partially closed union must never be used as a filter.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS, including `CapturedEvidenceHoldsNoLiveMaterialReference`.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs \
        Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs
git commit -m "feat: close material dependencies and freeze animation evidence"
```

---

## Task 11: Proof-relevant binding discovery for scalar, colour, and vector

Uses exactly the binding forms recorded in Task 2. `_Color.a` and `_MainTex_ST.x` must resolve to their parent colour and vector properties, never fall through scalar-only handling.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `CapturedAnimationEvidence.RelevanceRequest` from Task 10; Task 2's recorded forms.
- Produces:
  - `internal enum AnimatedPropertyKind { Scalar, ColorComponent, VectorComponent }`
  - `internal readonly struct AnimatedPropertyRef { internal int SlotIndex { get; } internal string PropertyName { get; } internal AnimatedPropertyKind Kind { get; } internal int ComponentIndex { get; } }`
  - `internal static bool UnityAnimationEvidenceCapture.TryResolveProofRelevant(CapturedFloatBinding binding, string rendererPath, MaterialEvidenceRequest relevance, out AnimatedPropertyRef reference)`

`ComponentIndex` is 0–3 in `r,g,b,a` order for colours and `x,y,z,w` order for vectors, and is `-1` for scalars.

- [ ] **Step 1: Write the failing test**

```csharp
        private static MaterialEvidenceRequest Relevance()
        {
            return new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: System.Array.Empty<string>(),
                scalarProperties: new[] { "_Cutoff" },
                colorProperties: new[] { "_Color" },
                vectorProperties: new[] { "_MainTex_ST" },
                textureProperties:
                    System.Array.Empty<TexturePropertyEvidenceRequest>());
        }

        private static CapturedFloatBinding Bound(string property)
        {
            return new CapturedFloatBinding(
                "Body", nameof(SkinnedMeshRenderer), property, true, new[] { 1f });
        }

        [Test]
        public void ScalarBindingResolvesAsScalar()
        {
            Assert.That(UnityAnimationEvidenceCapture.TryResolveProofRelevant(
                Bound("material._Cutoff"), "Body", Relevance(), out var reference),
                Is.True);
            Assert.That(reference.Kind, Is.EqualTo(AnimatedPropertyKind.Scalar));
            Assert.That(reference.PropertyName, Is.EqualTo("_Cutoff"));
            Assert.That(reference.SlotIndex, Is.Zero);
            Assert.That(reference.ComponentIndex, Is.EqualTo(-1));
        }

        [TestCase("material._Color.r", 0)]
        [TestCase("material._Color.g", 1)]
        [TestCase("material._Color.b", 2)]
        [TestCase("material._Color.a", 3)]
        public void ColourComponentResolvesToItsParent(string property, int component)
        {
            Assert.That(UnityAnimationEvidenceCapture.TryResolveProofRelevant(
                Bound(property), "Body", Relevance(), out var reference), Is.True);
            Assert.That(reference.Kind,
                Is.EqualTo(AnimatedPropertyKind.ColorComponent));
            Assert.That(reference.PropertyName, Is.EqualTo("_Color"));
            Assert.That(reference.ComponentIndex, Is.EqualTo(component));
        }

        [TestCase("material._MainTex_ST.x", 0)]
        [TestCase("material._MainTex_ST.w", 3)]
        public void VectorComponentResolvesToItsParent(string property, int component)
        {
            Assert.That(UnityAnimationEvidenceCapture.TryResolveProofRelevant(
                Bound(property), "Body", Relevance(), out var reference), Is.True);
            Assert.That(reference.Kind,
                Is.EqualTo(AnimatedPropertyKind.VectorComponent));
            Assert.That(reference.PropertyName, Is.EqualTo("_MainTex_ST"));
            Assert.That(reference.ComponentIndex, Is.EqualTo(component));
        }

        [Test]
        public void SlotIndexIsTakenFromTheBindingForm()
        {
            Assert.That(UnityAnimationEvidenceCapture.TryResolveProofRelevant(
                Bound("material[2]._Cutoff"), "Body", Relevance(), out var reference),
                Is.True);
            Assert.That(reference.SlotIndex, Is.EqualTo(2));
        }

        [Test]
        public void UnrequestedPropertiesAreNotRelevant()
        {
            Assert.That(UnityAnimationEvidenceCapture.TryResolveProofRelevant(
                Bound("material._Unrelated"), "Body", Relevance(), out _), Is.False);
            Assert.That(UnityAnimationEvidenceCapture.TryResolveProofRelevant(
                Bound("material._Unrelated.a"), "Body", Relevance(), out _), Is.False);
        }

        [Test]
        public void BindingsOnOtherRendererPathsAreNotRelevant()
        {
            Assert.That(UnityAnimationEvidenceCapture.TryResolveProofRelevant(
                Bound("material._Cutoff"), "Other", Relevance(), out _), Is.False);
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — the type and method are not defined.

- [ ] **Step 3: Implement resolution**

Match `Path` against the renderer's avatar-relative path with `StringComparison.Ordinal`. Parse the `material` prefix in exactly the form Task 2 recorded, extracting the slot index where present and defaulting to 0. Then: if the remaining name ends in a recorded colour suffix and its stem is in `ColorProperties`, produce `ColorComponent`; if it ends in a recorded vector suffix and its stem is in `VectorProperties`, produce `VectorComponent`; if the whole name is in `ScalarProperties`, produce `Scalar`; otherwise return false. A component suffix whose stem is not requested returns false — it must never degrade into a scalar match.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: resolve scalar, colour, and vector animated bindings"
```

---

## Task 12: Finite-exact singleton analysis for scalars

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `CapturedFloatBinding` from Task 10.
- Produces:
  - `internal enum AdmittedPropertyOutcome { Singleton, NotFiniteExact, SourcesDisagree }`
  - `internal static AdmittedPropertyOutcome AdmittedMaterialStates.AdmitScalar(IReadOnlyList<CapturedFloatBinding> bindings, float serializedDefault, out float admittedValue)`

- [ ] **Step 1: Write the failing test**

```csharp
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AdmittedMaterialStatesTests
    {
        private static CapturedFloatBinding Binding(
            bool finiteExact, params float[] values)
        {
            return new CapturedFloatBinding(
                "Body", "SkinnedMeshRenderer", "material._Cutoff",
                finiteExact, values);
        }

        [Test]
        public void AgreeingSourcesAdmitTheSingleValue()
        {
            var outcome = AdmittedMaterialStates.AdmitScalar(
                new[] { Binding(true, 1f), Binding(true, 1f) }, 1f, out var value);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(value, Is.EqualTo(1f));
        }

        [Test]
        public void DisagreeingSourcesRefuse()
        {
            Assert.That(AdmittedMaterialStates.AdmitScalar(
                new[] { Binding(true, 1f), Binding(true, 0f) }, 1f, out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void DisagreementWithTheSerializedDefaultRefuses()
        {
            Assert.That(AdmittedMaterialStates.AdmitScalar(
                new[] { Binding(true, 1f) }, 0f, out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void NonFiniteExactCurveRefuses()
        {
            Assert.That(AdmittedMaterialStates.AdmitScalar(
                new[] { Binding(false, 1f) }, 1f, out _),
                Is.EqualTo(AdmittedPropertyOutcome.NotFiniteExact));
        }

        [Test]
        public void MultipleKeyframeValuesInOneCurveRefuse()
        {
            Assert.That(AdmittedMaterialStates.AdmitScalar(
                new[] { Binding(true, 0f, 1f) }, 1f, out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `AdmittedMaterialStates` does not exist.

- [ ] **Step 3: Implement the singleton rule**

Return `NotFiniteExact` if any binding is not finite-exact. Otherwise gather every keyframe value plus the serialized default; if they are not all bit-identical, return `SourcesDisagree`; otherwise `Singleton`. Compare with `==` on `float`, never a tolerance — approximate equality would merge genuinely different states.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "feat: admit only singleton finite-exact scalar values"
```

---

## Task 13: Colour and vector reconstruction and singleton analysis

A colour or vector is animated component-wise. The admitted value is reconstructed from the serialized default with each animated component overridden by its own singleton. Any non-singleton component refuses the whole property.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `AdmitScalar` from Task 12; `AnimatedPropertyRef` from Task 11.
- Produces:
  - `internal static AdmittedPropertyOutcome AdmittedMaterialStates.AdmitColor(IReadOnlyDictionary<int, IReadOnlyList<CapturedFloatBinding>> componentBindings, Color serializedDefault, out Color admittedValue)`
  - `internal static AdmittedPropertyOutcome AdmittedMaterialStates.AdmitVector(IReadOnlyDictionary<int, IReadOnlyList<CapturedFloatBinding>> componentBindings, Vector4 serializedDefault, out Vector4 admittedValue)`

- [ ] **Step 1: Write the failing test**

```csharp
        private static IReadOnlyDictionary<int, IReadOnlyList<CapturedFloatBinding>>
            Components(params (int Index, float Value)[] entries)
        {
            var map = new Dictionary<int, IReadOnlyList<CapturedFloatBinding>>();
            foreach (var entry in entries)
            {
                map[entry.Index] = new[] { Binding(true, entry.Value) };
            }

            return map;
        }

        [Test]
        public void UnanimatedColourComponentsComeFromTheSerializedDefault()
        {
            var outcome = AdmittedMaterialStates.AdmitColor(
                Components((3, 1f)),
                new Color(0.1f, 0.2f, 0.3f, 0.4f),
                out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted.r, Is.EqualTo(0.1f));
            Assert.That(admitted.g, Is.EqualTo(0.2f));
            Assert.That(admitted.b, Is.EqualTo(0.3f));
            Assert.That(admitted.a, Is.EqualTo(1f), "the animated alpha must win");
        }

        [Test]
        public void AnyNonSingletonComponentRefusesTheWholeColour()
        {
            var map = new Dictionary<int, IReadOnlyList<CapturedFloatBinding>>
            {
                [3] = new[] { Binding(true, 1f), Binding(true, 0f) },
            };

            Assert.That(AdmittedMaterialStates.AdmitColor(
                map, Color.white, out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void AlphaComponentIsNeverTreatedAsAScalarProperty()
        {
            // _Color.a must flow through AdmitColor. If it degraded into scalar
            // handling, the other three components would be lost.
            var outcome = AdmittedMaterialStates.AdmitColor(
                Components((3, 0f)),
                new Color(1f, 1f, 1f, 1f),
                out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(new Color(1f, 1f, 1f, 0f)));
        }

        [Test]
        public void VectorComponentsReconstructFromTheSerializedDefault()
        {
            var outcome = AdmittedMaterialStates.AdmitVector(
                Components((0, 2f), (1, 3f)),
                new Vector4(1f, 1f, 0f, 0f),
                out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(new Vector4(2f, 3f, 0f, 0f)));
        }

        [Test]
        public void NonFiniteExactVectorComponentRefuses()
        {
            var map = new Dictionary<int, IReadOnlyList<CapturedFloatBinding>>
            {
                [0] = new[] { Binding(false, 1f) },
            };

            Assert.That(AdmittedMaterialStates.AdmitVector(
                map, Vector4.zero, out _),
                Is.EqualTo(AdmittedPropertyOutcome.NotFiniteExact));
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `AdmitColor` and `AdmitVector` not defined.

- [ ] **Step 3: Implement reconstruction**

Start from the serialized default. For each present component index 0–3, call `AdmitScalar` with that component's bindings and that component of the default as the serialized value. Any component returning `NotFiniteExact` returns `NotFiniteExact` for the property; any returning `SourcesDisagree` returns `SourcesDisagree`; otherwise write the admitted component value and return `Singleton`. Component indices outside 0–3 are a defect: throw `ArgumentOutOfRangeException`.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "feat: reconstruct admitted colour and vector properties"
```

---

## Task 14: Authorization-grade behaviour identity and allowlist

`Type.FullName` alone is not authorization-grade: two assemblies can define identically named types.

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/BehaviourIdentity.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/BehaviourIdentityTests.cs`

**Interfaces:**
- Consumes: `AvatarAnimationRefusal` from Task 7.
- Produces:
  - `internal static string BehaviourIdentity.Of(Type type)` returning `"<package-name>@<package-version>|<assembly-name>|<type-full-name>"`, with `"<no-package>"` substituted when the defining assembly maps to no package.
  - `internal static bool BehaviourIdentity.IsAllowed(string identity)`
  - `internal static readonly IReadOnlyCollection<string> BehaviourIdentity.AllowedIdentities`

Package name and version come from `UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly)`, which needs no VRChat SDK reference.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class BehaviourIdentityTests
    {
        [Test]
        public void IdentityIncludesAssemblyAndPackage()
        {
            var identity = BehaviourIdentity.Of(typeof(BehaviourIdentityTests));

            Assert.That(identity, Does.Contain(
                typeof(BehaviourIdentityTests).Assembly.GetName().Name));
            Assert.That(identity, Does.Contain(
                typeof(BehaviourIdentityTests).FullName));
        }

        [Test]
        public void AllowlistStartsEmpty()
        {
            // The spec requires an empty allowlist that grows only with a recorded
            // per-type justification. A non-empty list here means a type was added
            // without it.
            Assert.That(BehaviourIdentity.AllowedIdentities, Is.Empty);
        }

        [Test]
        public void UnknownIdentityIsNotAllowed()
        {
            Assert.That(BehaviourIdentity.IsAllowed(
                "some.package@1.0.0|SomeAsm|SomeVendor.MysteryBehaviour"), Is.False);
        }

        [Test]
        public void NullOrEmptyIdentityIsNotAllowed()
        {
            Assert.That(BehaviourIdentity.IsAllowed(null), Is.False);
            Assert.That(BehaviourIdentity.IsAllowed(string.Empty), Is.False);
        }

        [Test]
        public void IdenticallyNamedTypeFromAnotherAssemblyCannotBeSpoofed()
        {
            // Two distinct types deliberately sharing a full name. Only one may
            // ever be allowlisted; a FullName-only check would authorize both.
            var first = typeof(SpoofProbe.Duplicate);
            var second = BuildDuplicateInAnotherAssembly();

            Assert.That(first.FullName, Is.EqualTo(second.FullName),
                "fixture precondition: the names must collide");
            Assert.That(BehaviourIdentity.Of(first),
                Is.Not.EqualTo(BehaviourIdentity.Of(second)),
                "identity must distinguish two same-named types from different " +
                "assemblies");
        }
    }
}
```

Implement `BuildDuplicateInAnotherAssembly()` with `System.Reflection.Emit` to define a dynamic assembly containing a type whose full name equals `SpoofProbe.Duplicate`, and declare a matching `SpoofProbe.Duplicate` class in the test assembly. This is test-only reflection to build a fixture, not reflection used to authorize a production path, and is permitted. If `Reflection.Emit` is unavailable in this Unity's scripting backend, fall back to asserting that `BehaviourIdentity.Of` includes the assembly name and that two different assembly names yield different identities, and record the limitation in the test file header.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `BehaviourIdentity` does not exist.

- [ ] **Step 3: Implement identity and the empty allowlist**

`AllowedIdentities` is an empty `IReadOnlyCollection<string>` with a file-level comment stating that an identity may be added only with a recorded justification that the type's effect is confined to parameters, layer or playable weights, or state selection, and that this is verification obligation 7. `IsAllowed` returns false for null, empty, and anything not in the set, comparing with `StringComparison.Ordinal`.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Wire the avatar-scoped refusal**

In `CommittedControllerGraph.Enumerate`, return `AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour` when any collected behaviour's identity is not allowed, with empty `Layers`. Add a test in `CommittedControllerGraphTests` that attaches a locally declared `StateMachineBehaviour` subclass to a state and asserts that refusal.

- [ ] **Step 6: Run and commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/BehaviourIdentity.cs \
        Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/BehaviourIdentityTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs
git commit -m "feat: authorize behaviours by assembly-qualified identity"
```

---

## Task 15: Structural invalidation

Uses the binding **category** recorded in Task 3 for `m_Materials.Array.size`. Do not assume it is an object binding.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Consumes: `CapturedFloatBinding` and `CapturedObjectBinding` from Task 10; Task 3's recorded category.
- Produces:
  - two new `RendererAnalysisRefusal` members, `AnimatedMeshReplacement` and `AnimatedMaterialSlotCount`;
  - `internal static RendererAnalysisRefusal UnityRendererAlphaAnalysis.StructuralRefusalFor(IReadOnlyList<CapturedFloatBinding> floats, IReadOnlyList<CapturedObjectBinding> objects, string rendererPath)`

Passing both lists is what lets the slot-count check live in whichever category Task 3 observed.

- [ ] **Step 1: Write the failing test**

```csharp
        private static CapturedObjectBinding Obj(string property)
        {
            return new CapturedObjectBinding(
                "Body", nameof(SkinnedMeshRenderer), property,
                System.Array.Empty<int>());
        }

        private static CapturedFloatBinding Flt(string property)
        {
            return new CapturedFloatBinding(
                "Body", nameof(SkinnedMeshRenderer), property, true, new[] { 2f });
        }

        [Test]
        public void AnimatedMeshReplacementRefusesTheRenderer()
        {
            Assert.That(UnityRendererAlphaAnalysis.StructuralRefusalFor(
                System.Array.Empty<CapturedFloatBinding>(),
                new[] { Obj("m_Mesh") },
                "Body"),
                Is.EqualTo(RendererAnalysisRefusal.AnimatedMeshReplacement));
        }

        [Test]
        public void AnimatedSlotCountRefusesTheRendererInEitherCategory()
        {
            // Whichever category Task 3 recorded, the refusal must fire.
            Assert.That(UnityRendererAlphaAnalysis.StructuralRefusalFor(
                new[] { Flt("m_Materials.Array.size") },
                System.Array.Empty<CapturedObjectBinding>(),
                "Body"),
                Is.EqualTo(RendererAnalysisRefusal.AnimatedMaterialSlotCount));

            Assert.That(UnityRendererAlphaAnalysis.StructuralRefusalFor(
                System.Array.Empty<CapturedFloatBinding>(),
                new[] { Obj("m_Materials.Array.size") },
                "Body"),
                Is.EqualTo(RendererAnalysisRefusal.AnimatedMaterialSlotCount));
        }

        [Test]
        public void OrdinarySlotSwapIsNotAStructuralRefusal()
        {
            Assert.That(UnityRendererAlphaAnalysis.StructuralRefusalFor(
                System.Array.Empty<CapturedFloatBinding>(),
                new[] { Obj("m_Materials.Array.data[0]") },
                "Body"),
                Is.EqualTo(RendererAnalysisRefusal.None));
        }

        [Test]
        public void StructuralBindingsOnOtherPathsAreIgnored()
        {
            var elsewhere = new CapturedObjectBinding(
                "Other", nameof(SkinnedMeshRenderer), "m_Mesh",
                System.Array.Empty<int>());

            Assert.That(UnityRendererAlphaAnalysis.StructuralRefusalFor(
                System.Array.Empty<CapturedFloatBinding>(),
                new[] { elsewhere },
                "Body"),
                Is.EqualTo(RendererAnalysisRefusal.None));
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — enum members and method not defined.

- [ ] **Step 3: Implement**

Append the two members to `RendererAnalysisRefusal`, preserving the documented rule that declaration order is check order. Match `m_Mesh` and `m_Materials.Array.size` on the renderer's own path in **both** lists, so the check is correct under whichever category Task 3 recorded.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs
git commit -m "feat: refuse renderers with animated structural invalidation"
```

---

## Task 16: Admitted-state product budgeting

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `internal static bool AdmittedMaterialStates.TryBudgetProduct(IReadOnlyList<int> perSlotAdmittedCounts, out int productSize)`

The cap is a `private const int` inside `AdmittedMaterialStates`. It must not be `public` or `internal`, must not appear in any signature, and must not be asserted as a number outside this task.

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
            var counts = new int[64];
            for (var index = 0; index < counts.Length; index++)
            {
                counts[index] = 4;
            }

            Assert.That(AdmittedMaterialStates.TryBudgetProduct(counts, out _),
                Is.False);
        }

        [Test]
        public void BudgetingDoesNotOverflowOnHugeCounts()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { int.MaxValue, int.MaxValue }, out _), Is.False);
        }

        [Test]
        public void AZeroCountYieldsAnEmptyProduct()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 3, 0 }, out var size), Is.True);
            Assert.That(size, Is.Zero);
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement**

Multiply into a `long` accumulator and return false as soon as it exceeds the cap, so no oversized product is materialized and no overflow occurs.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Add the refusal member**

Add `AdmittedStateBudgetExceeded` to `RendererAnalysisRefusal` with a test asserting the renderer refuses with it.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
        Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "feat: bound the admitted state product before materialization"
```

---

## Task 17: Presence-preserving immutable evidence substitution

The single new pure operation the spec allows. **Substitution must never invent a property that the captured material does not have.**

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `CapturedMaterialEvidence`; Task 5's recorded absent-property rule.
- Produces:
  - `internal CapturedMaterialEvidence CapturedMaterialEvidence.WithScalar(string name, float value)`
  - `internal CapturedMaterialEvidence CapturedMaterialEvidence.WithColor(string name, Color value)`
  - `internal CapturedMaterialEvidence CapturedMaterialEvidence.WithVector(string name, Vector4 value)`

**Presence rule.** Each entry carries both a requested-ness and a captured `HasValue`. Substitution:
- **throws `ArgumentException`** when the name was never requested — substituting an unrequested property would invent a fact, which is a defect, not a domain outcome;
- when the name was requested but the captured entry has `HasValue == false` (the material does not have the property), **follow the branch Task 5 recorded**: if absent stays absent and the binding is ineffective, leave `HasValue` false and ignore the substituted value; if Task 5 observed that assignment makes the property effective, do not silently flip the flag — instead add `RendererAnalysisRefusal.AnimatedPropertyAbsentFromAdmittedMaterial` and refuse that admitted state;
- when the entry has `HasValue == true`, replace the value and keep `HasValue` true.

Setting `HasValue = true` merely because a substitution occurred is never acceptable.

- [ ] **Step 1: Write the failing test**

```csharp
        private static CapturedMaterialEvidence CaptureWith(
            Material material, params string[] scalars)
        {
            var request = new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: System.Array.Empty<string>(),
                scalarProperties: scalars,
                colorProperties: new[] { "_Color" },
                vectorProperties: new[] { "_MainTex_ST" },
                textureProperties:
                    System.Array.Empty<TexturePropertyEvidenceRequest>());
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, request),
            })[0];
        }

        [Test]
        public void ScalarSubstitutionDerivesANewValueWithoutMutatingTheSource()
        {
            var material = new Material(Shader.Find("Standard"));
            try
            {
                var captured = CaptureWith(material, "_Cutoff");
                captured.TryGetScalar("_Cutoff", out var before);

                var substituted = captured.WithScalar("_Cutoff", 0.25f);

                Assert.That(substituted, Is.Not.SameAs(captured));
                Assert.That(substituted.TryGetScalar("_Cutoff", out var after),
                    Is.True);
                Assert.That(after, Is.EqualTo(0.25f));

                captured.TryGetScalar("_Cutoff", out var unchanged);
                Assert.That(unchanged, Is.EqualTo(before),
                    "substitution mutated the source evidence");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SubstitutingAnUnrequestedPropertyThrows()
        {
            var material = new Material(Shader.Find("Standard"));
            try
            {
                var captured = CaptureWith(material, "_Cutoff");

                Assert.That(() => captured.WithScalar("_NotRequested", 1f),
                    Throws.TypeOf<System.ArgumentException>());
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SubstitutionDoesNotInventPresenceOnAMaterialWithoutTheProperty()
        {
            // Unlit/Color has no _Cutoff, so the captured entry has no value.
            var material = new Material(Shader.Find("Unlit/Color"));
            try
            {
                var captured = CaptureWith(material, "_Cutoff");
                Assert.That(captured.TryGetScalar("_Cutoff", out _), Is.False,
                    "fixture precondition: the property must be absent");

                var substituted = captured.WithScalar("_Cutoff", 0.25f);

                Assert.That(substituted.TryGetScalar("_Cutoff", out _), Is.False,
                    "substitution invented a property the material does not have");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ColourAndVectorSubstitutionPreservePresence()
        {
            var material = new Material(Shader.Find("Standard"));
            try
            {
                var captured = CaptureWith(material, "_Cutoff");

                var colour = captured.WithColor("_Color", new Color(0f, 0f, 0f, 0.5f));
                Assert.That(colour.TryGetColor("_Color", out var value), Is.True);
                Assert.That(value.a, Is.EqualTo(0.5f));

                var vector = captured.WithVector(
                    "_MainTex_ST", new Vector4(2f, 2f, 0f, 0f));
                Assert.That(vector.TryGetVector("_MainTex_ST", out var st), Is.True);
                Assert.That(st.x, Is.EqualTo(2f));
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

Copy the private entry arrays, replace the matching entry's value while **preserving its `HasValue` flag exactly**, and construct a new `CapturedMaterialEvidence` over the copies. Throw `ArgumentException` for an unrequested name, reusing the existing `Unrequested(name)` message convention.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS. If `SubstitutionDoesNotInventPresenceOnAMaterialWithoutTheProperty` cannot pass because Task 5 recorded that assignment makes a property effective, implement the refusal branch instead and change this test to assert that refusal.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs
git commit -m "feat: derive captured evidence with presence-preserving substitution"
```

---

## Task 18: Resolve admitted states and deduplicate exactly

Dedup is **performance-only**. Correctness is defined over all distinct semantic resolutions.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `AlphaSemanticsResolver.Resolve` (unchanged); the substitution methods from Task 17.
- Produces: `internal static IReadOnlyList<AlphaResolution> AdmittedMaterialStates.DistinctResolutions(IReadOnlyList<AlphaResolution> resolutions)`

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void IdenticalUniformResolutionsCollapse()
        {
            var distinct = AdmittedMaterialStates.DistinctResolutions(new[]
            {
                AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque),
                AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque),
            });

            Assert.That(distinct.Count, Is.EqualTo(1));
        }

        [Test]
        public void DifferentUniformOutcomesDoNotCollapse()
        {
            var distinct = AdmittedMaterialStates.DistinctResolutions(new[]
            {
                AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque),
                AlphaResolution.Uniform(TriangleAlphaOutcome.MustRemainTransparent),
            });

            Assert.That(distinct.Count, Is.EqualTo(2));
        }

        [Test]
        public void UniformAndRefusedNeverMerge()
        {
            var distinct = AdmittedMaterialStates.DistinctResolutions(new[]
            {
                AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque),
                AlphaResolution.Refused(AlphaResolutionFailure.SemanticsUnknown),
            });

            Assert.That(distinct.Count, Is.EqualTo(2));
        }

        [Test]
        public void DedupIsPerformanceOnlyAndPreservesTheOutcome()
        {
            var full = new[]
            {
                AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque),
                AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque),
                AlphaResolution.Uniform(TriangleAlphaOutcome.MustRemainTransparent),
            };

            var distinct = AdmittedMaterialStates.DistinctResolutions(full);

            // Every outcome reachable from the full list must remain reachable.
            Assert.That(distinct.Count, Is.LessThan(full.Length));
            Assert.That(
                distinct.Select(r => r.Classify(TriangleAlphaInput.MissingUv0(
                    Vector3.zero, Vector3.right, Vector3.up))).Distinct().Count(),
                Is.EqualTo(full.Select(r => r.Classify(TriangleAlphaInput.MissingUv0(
                    Vector3.zero, Vector3.right, Vector3.up))).Distinct().Count()));
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement conservative dedup**

Merge only when both resolutions are uniform with the same outcome, or both refused with the same failure. **Never merge two classified resolutions**, even when their fields look equal: reference-distinct `AlphaTextureData` cannot be proven equivalent cheaply, and keeping them separate costs work but never correctness. State exactly that in a code comment.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "feat: deduplicate admitted resolutions conservatively"
```

---

## Task 19: Per-triangle intersection

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Consumes: `DistinctResolutions` from Task 18; the existing private `Classify` helper.
- Produces: `internal static TriangleAlphaOutcome[] UnityRendererAlphaAnalysis.IntersectOutcomes(IReadOnlyList<TriangleAlphaOutcome[]> perResolutionOutcomes)`

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void OnlyTrianglesOpaqueInEveryStateStayOpaque()
        {
            var intersected = UnityRendererAlphaAnalysis.IntersectOutcomes(new[]
            {
                new[]
                {
                    TriangleAlphaOutcome.ProvenOpaque,
                    TriangleAlphaOutcome.ProvenOpaque,
                },
                new[]
                {
                    TriangleAlphaOutcome.ProvenOpaque,
                    TriangleAlphaOutcome.MustRemainTransparent,
                },
            });

            Assert.That(intersected[0], Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(intersected[1],
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void UnknownInAnyStateRemovesOpacity()
        {
            var intersected = UnityRendererAlphaAnalysis.IntersectOutcomes(new[]
            {
                new[] { TriangleAlphaOutcome.ProvenOpaque },
                new[] { TriangleAlphaOutcome.Unknown },
            });

            Assert.That(intersected[0],
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void AnEmptyResolutionSetIsADefectNotAnOpaqueResult()
        {
            Assert.That(() => UnityRendererAlphaAnalysis.IntersectOutcomes(
                System.Array.Empty<TriangleAlphaOutcome[]>()),
                Throws.TypeOf<System.ArgumentException>());
        }

        [Test]
        public void MismatchedOutcomeLengthsAreADefect()
        {
            Assert.That(() => UnityRendererAlphaAnalysis.IntersectOutcomes(new[]
            {
                new[] { TriangleAlphaOutcome.ProvenOpaque },
                new[]
                {
                    TriangleAlphaOutcome.ProvenOpaque,
                    TriangleAlphaOutcome.ProvenOpaque,
                },
            }), Throws.TypeOf<System.ArgumentException>());
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement**

A triangle is `ProvenOpaque` only when every array reports `ProvenOpaque` at that index; `MustRemainTransparent` when every array agrees on that; `Unknown` otherwise. Throw `ArgumentException` for an empty list and for arrays of differing length — both are defects, and an empty list must never yield `ProvenOpaque` by vacuous truth.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs
git commit -m "feat: intersect triangle outcomes across admitted states"
```

---

## Task 20: Recursive no-live-Unity-object evidence guard

Deferred PlatformFinish finding 1.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs`

**Interfaces:**
- Consumes: every captured evidence type from Task 10.
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

        [Test]
        public void TransientObservationIsDeliberatelyExemptAndDocumented()
        {
            // LiveAnimationObservation intentionally holds live references and is
            // confined to host capture. This test pins that it is NOT evidence, so
            // a future refactor cannot quietly promote it into the proof path.
            Assert.That(() => AssertHasNoUnityObjectFields(
                typeof(LiveObjectObservation)),
                Throws.InstanceOf<AssertionException>(),
                "LiveObjectObservation must remain transient host-only");
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `GuardCatchesUnityObjectsNestedBelowTheFirstLevel` passes wrongly under the shallow guard, so its `Throws` assertion fails.

- [ ] **Step 3: Generalize the guard**

Walk fields recursively with a visited-type set to terminate on cycles, descending into generic arguments and array element types. Fail on any field whose type derives from `UnityEngine.Object`. Skip primitives and `string`.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS, including the pre-existing snapshot guard tests, which must not regress.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs
git commit -m "test: walk the whole captured graph in the Unity object guard"
```

---

## Task 21: Named refusal and failure boundary

Deferred PlatformFinish finding I4.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: `AvatarAnimationRefusal` from Task 7; `BehaviourIdentity` from Task 14.
- Produces: `AmusePlatformFinishState.AvatarRefusal` and per-refusal renderer counters.

- [ ] **Step 1: Write the failing test**

The avatar-scoped test must construct a **real unallowlisted behaviour** and assert the specific refusal with zero renderer analysis.

```csharp
        internal sealed class UnallowlistedProbeBehaviour : StateMachineBehaviour
        {
        }

        [Test]
        public void UnallowlistedBehaviourRefusesTheWholeAvatarWithoutAnalysis()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE unallowlisted behaviour fixture");
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("L0");
                var state = controller.layers[0].stateMachine.AddState("S0");
                state.AddStateMachineBehaviour<UnallowlistedProbeBehaviour>();
                root.AddComponent<Animator>().runtimeAnimatorController = controller;

                // A renderer that would otherwise analyze successfully, so a zero
                // analyzed count proves the refusal stopped analysis rather than
                // there being nothing to analyze.
                AddAnalyzableRenderer(root);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var state2 = context.GetState<AmusePlatformFinishState>();

                Assert.That(state2.HasExecuted, Is.True);
                Assert.That(state2.AvatarRefusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour));
                Assert.That(state2.AnalyzedRendererCount, Is.Zero,
                    "an avatar-scoped refusal must analyze no renderer");
                Assert.That(state2.OpaqueCandidateTriangleCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(controller);
            }
        }

        [Test]
        public void AnalyzableAvatarWithoutBehavioursIsNotRefused()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE clean avatar fixture");
            try
            {
                AddAnalyzableRenderer(root);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var state = context.GetState<AmusePlatformFinishState>();

                Assert.That(state.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(state.AnalyzedRendererCount, Is.GreaterThan(0),
                    "control case: this avatar must actually analyze");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
```

Write `AddAnalyzableRenderer` as a private helper building a triangle mesh with an attested material through the existing fixture helpers. Add a third test that arms a renderer whose analysis throws a deliberately constructed defect and asserts the exception escapes `ProcessAvatar` rather than being swallowed into a refusal counter.

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

## Task 22: Integrate into the PlatformFinish analysis path

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
            var withoutSwap = new GameObject("AMUSE control fixture");
            var withSwap = new GameObject("AMUSE admitted swap fixture");
            try
            {
                AddAnalyzableRenderer(withoutSwap);
                var control = AvatarProcessor.ProcessAvatar(
                    withoutSwap, TestVrchatPlatform.Instance);
                var controlOpaque = control
                    .GetState<AmusePlatformFinishState>()
                    .OpaqueCandidateTriangleCount;

                // Control must actually prove something, or the comparison below
                // would pass vacuously.
                Assert.That(controlOpaque, Is.GreaterThan(0));

                AddAnalyzableRenderer(withSwap);
                AddAnimatedSwapToTransparentMaterial(withSwap);
                var swapped = AvatarProcessor.ProcessAvatar(
                    withSwap, TestVrchatPlatform.Instance);

                Assert.That(swapped
                    .GetState<AmusePlatformFinishState>()
                    .OpaqueCandidateTriangleCount, Is.Zero,
                    "a face opaque only in the current state must not be counted");
            }
            finally
            {
                Object.DestroyImmediate(withoutSwap);
                Object.DestroyImmediate(withSwap);
            }
        }
```

Write `AddAnimatedSwapToTransparentMaterial` as a private helper attaching an `Animator` whose clip carries an `m_Materials.Array.data[0]` object curve keying an attested transparent material. Both materials must resolve through real frontends; a stock Unity shader resolves all-Unknown and would make the test pass vacuously.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — the capture pass does not exist and the barrier ignores animation.

- [ ] **Step 3: Implement the two passes**

In `AmusePlatformFinishPlugin.Configure`:

```csharp
        protected override void Configure()
        {
            var sequence = InPhase(BuildPhase.PlatformFinish);

            sequence.WithRequiredExtension(
                typeof(AnimatorServicesContext),
                inner => inner.Run(
                    BindingsCapturePassName, AmuseAnimatorBindingsCapture.Execute));

            sequence.Run(BarrierPassName, AmusePlatformFinishPass.Execute);
        }
```

`AmuseAnimatorBindingsCapture.Execute` stores `context.Extension<AnimatorServicesContext>().ControllerContext.PlatformBindings` — plus, under Task 6's conservative branch if it was taken, the innate `(key, controller)` pairs — into `AmusePlatformFinishState`. The barrier pass declares no animator extension and wires: enumerate the committed graph, observe transiently, close dependencies, construct admitted states, budget, resolve, deduplicate, classify, intersect.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Run the complete EditMode suite**

Confirm no regression against the recorded baseline. The three NDMF Harmony `mprotect returned EACCES` console entries are the known environment baseline and are not failures.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseAnimatorBindingsCapture.cs \
        Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: prove alpha opacity across admitted runtime states"
```

---

## Plan self-review record

**Spec requirement trace.** Each requirement is traced to the concrete produced type and task that carries it.

| Spec requirement | Produced type / member | Task |
| --- | --- | --- |
| Scalar property admission | `AdmittedMaterialStates.AdmitScalar`, `AnimatedPropertyKind.Scalar` | 11, 12 |
| Colour property admission | `AdmittedMaterialStates.AdmitColor`, `AnimatedPropertyKind.ColorComponent`, `CapturedMaterialEvidence.WithColor` | 11, 13, 17 |
| Vector property admission | `AdmittedMaterialStates.AdmitVector`, `AnimatedPropertyKind.VectorComponent`, `CapturedMaterialEvidence.WithVector` | 11, 13, 17 |
| Material swaps | `LiveAnimationObservation.TryParseMaterialSlotBinding`, `CapturedObjectBinding.AdmittedMaterialIndices` | 9, 10 |
| Texture/object-reference animation | Task 3 decides existence; if present, escalated to the user as a design change before Task 10 | 3 |
| Structural invalidation | `RendererAnalysisRefusal.AnimatedMeshReplacement`, `.AnimatedMaterialSlotCount`, `StructuralRefusalFor` | 3, 15 |
| Behaviour authorization | `BehaviourIdentity.Of`, `.IsAllowed`, `AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour` | 14, 21 |
| Live-object to immutable transition | `LiveAnimationObservation` (live, transient) → `UnityAnimationEvidenceCapture.Capture` → `CapturedAnimationEvidence` (immutable), enforced by `AssertHasNoUnityObjectFields` | 8, 10, 20 |
| Dependency closure ordering | `CapturedAnimationEvidence.IsClosed`, `.RelevanceRequest` | 10 |
| Singleton rule | `AdmittedPropertyOutcome` | 12, 13 |
| Budget | `TryBudgetProduct`, `RendererAnalysisRefusal.AdmittedStateBudgetExceeded` | 16 |
| Dedup as performance-only | `DistinctResolutions` | 18 |
| Per-triangle intersection | `IntersectOutcomes` | 19 |
| Failure semantics | `AmusePlatformFinishState.AvatarRefusal`, no `catch` around renderer analysis | 21 |
| Observation boundary | `Sequence.WithRequiredExtension` capture pass, extension-free barrier | 1, 22 |
| Special motions never gate | `CapturedClipEvidence.IsSpecialMotion` is diagnostic-only | 10 |

**Placeholders.** None. Task 2 Step 3, Task 3 Step 4, Task 5 Step 3, and Task 6 Step 4 deliberately instruct recording an observed value — the defined purpose of a characterization test. Task 3 explicitly requires the disjunctive assertion to be replaced by an exact one before the task completes.

**Type consistency.** `AvatarAnimationRefusal` (7) is used in 14, 21, 22. `LiveFloatObservation`/`LiveObjectObservation`/`LiveClipObservation` (8) are consumed only by 9 and 10 and never after. `CapturedFloatBinding`/`CapturedObjectBinding` (10) are used in 11, 12, 13, 15, 20. `AnimatedPropertyRef` and `AnimatedPropertyKind` (11) feed 13. `AdmitScalar` (12) is called by `AdmitColor`/`AdmitVector` (13). `WithScalar`/`WithColor`/`WithVector` (17) feed 18. `DistinctResolutions` (18) feeds 19.

**Known open risks carried into execution.**
- Task 2's recorded binding forms drive Task 11's parser; a different component-suffix form changes that parser.
- Task 3 may find texture-reference object curves exist. That is a **design change**, not a plan change, and must go back to the user before Task 10.
- Task 5 selects between Task 17's presence-preserving branch and its refusal branch.
- Task 6 selects between Task 7 calling `GetInnateControllers` directly and the conservative recorded-pairs path.
- Obligation 4 (`MaterialPropertyBlock` application) may need Play Mode and can remain open; the conservative singleton rule does not depend on it.
- Task 7's `m_NormalizedBlendValues` serialized access is the only route found in this Unity version; re-check for a public accessor first.
- Task 14's spoof fixture needs `Reflection.Emit`; a documented fallback is specified if unavailable.
