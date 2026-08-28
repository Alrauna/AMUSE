using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Tests.Editor.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Host
        .AnimatorServicesReactivationCharacterizationTests
        .ReactivationCharacterizationPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Characterizes the lifecycle a material-swap rewrite would depend on:
    /// activate <see cref="AnimatorServicesContext"/>, deactivate it so NDMF
    /// commits the controller graph, analyze the committed graph, then
    /// <em>reactivate</em> the context and edit virtual object-reference curves
    /// through <see cref="AnimationIndex"/> before NDMF commits a second time.
    ///
    /// <para>
    /// Pinned source (NDMF 1.14.4) says this should work — <c>BuildContext</c>
    /// retains extension instances in <c>_extensions</c> across deactivation and
    /// <c>VirtualControllerContext.LayerState.Revalidate</c> exists precisely to
    /// re-enter — but "the API appears to allow it" is not evidence. This proves
    /// the sequence end to end on a synthetic avatar and, critically, that clip
    /// association is by binding and virtual-clip identity rather than by clip
    /// display name.
    /// </para>
    ///
    /// <para>
    /// This is a characterization of NDMF, not a test of AMUSE production code:
    /// no AMUSE production type participates, and nothing here is a contract
    /// AMUSE offers.
    /// </para>
    /// </summary>
    public sealed class AnimatorServicesReactivationCharacterizationTests
    {
        internal const string ReactivationPlatformName =
            "com.alrauna.amuse.tests.animator-reactivation";

        internal const string RendererPath = "swapped renderer";
        internal const string OtherRendererPath = "other renderer";
        internal const string SlotZeroBinding = "m_Materials.Array.data[0]";

        /// <summary>
        /// Two clips deliberately share this name. Nothing in the rewrite may
        /// associate a clip by it.
        /// </summary>
        internal const string CollidingClipName = "shared clip name";

        /// <summary>
        /// The prepared source-to-opaque mapping, keyed by reference. It stands
        /// in for the mapping a real preparation phase would build from the
        /// closed admitted set, and lives here rather than on the probe because
        /// the <see cref="BuildContext"/> does not exist until the build starts.
        /// </summary>
        internal static IReadOnlyDictionary<Material, Material> PreparedMapping
        {
            get;
            private set;
        }

        internal sealed class PreparedMappingScope : IDisposable
        {
            private readonly IReadOnlyDictionary<Material, Material> previous;

            internal PreparedMappingScope(
                IReadOnlyDictionary<Material, Material> mapping)
            {
                previous = PreparedMapping;
                PreparedMapping = mapping;
            }

            public void Dispose()
            {
                PreparedMapping = previous;
            }
        }

        internal sealed class ReactivationProbe
        {
            internal bool FirstWindowRan;
            internal bool ContextActiveInFirstWindow;

            internal bool BarrierRan;
            internal bool ContextInactiveAtBarrier;

            /// <summary>
            /// The committed slot-0 curve as read from the real committed
            /// AnimationClip, outside any animator-services window.
            /// </summary>
            internal string BarrierCommittedCurve;

            internal bool SecondWindowRan;
            internal bool ContextActiveInSecondWindow;

            /// <summary>
            /// How many virtual clips the reactivated index associated with the
            /// exact slot-0 binding. Two clips share a name; only one carries
            /// this binding.
            /// </summary>
            internal int ClipsForBindingInSecondWindow;

            /// <summary>
            /// The slot-0 curve as the reactivated index observed it, before any
            /// edit. Compared against <see cref="BarrierCommittedCurve"/>.
            /// </summary>
            internal string SecondWindowObservedCurve;

            internal readonly List<string> EditedClipNames = new List<string>();
            internal int MappedKeyframeCount;
            internal bool EveryValueWasMapped = true;

            internal Exception Failure;
        }

        [RunsOnPlatforms(ReactivationPlatformName)]
        public sealed class ReactivationCharacterizationPlugin :
            Plugin<ReactivationCharacterizationPlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.animator-reactivation";

            protected override void Configure()
            {
                // Extension declarations are evaluated in Configure, not in the
                // pass body, so an arming flag cannot stop this plugin from
                // activating and committing AnimatorServicesContext on every
                // build in the session. It is confined to its own platform
                // instead, which no other fixture uses.
                var sequence = InPhase(BuildPhase.PlatformFinish);

                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run("first animator window", FirstWindow));

                // No extension declared: NDMF deactivates and commits before
                // this pass, exactly as the production AMUSE barrier does.
                sequence.Run("committed-graph barrier", Barrier);

                // The sequence under characterization: a SECOND window.
                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run("second animator window", SecondWindow));
            }

            private static void FirstWindow(BuildContext context)
            {
                var probe = context.GetState<ReactivationProbe>();
                probe.FirstWindowRan = true;
                probe.ContextActiveInFirstWindow =
                    IsAnimatorServicesContextActive(context);
            }

            private static void Barrier(BuildContext context)
            {
                var probe = context.GetState<ReactivationProbe>();
                probe.BarrierRan = true;
                probe.ContextInactiveAtBarrier =
                    !IsAnimatorServicesContextActive(context);

                try
                {
                    probe.BarrierCommittedCurve = DescribeCommittedSlotCurve(
                        context.AvatarRootObject, RendererPath);
                }
                catch (Exception exception)
                {
                    probe.Failure ??= exception;
                }
            }

            private static void SecondWindow(BuildContext context)
            {
                var probe = context.GetState<ReactivationProbe>();
                probe.SecondWindowRan = true;
                probe.ContextActiveInSecondWindow =
                    IsAnimatorServicesContextActive(context);

                try
                {
                    var index = context.Extension<AnimatorServicesContext>()
                        .AnimationIndex;

                    // Binding identity, never clip name. The type is part of
                    // EditorCurveBinding equality, so it must match what the
                    // committed clip actually carries.
                    var binding = EditorCurveBinding.PPtrCurve(
                        RendererPath, typeof(SkinnedMeshRenderer), SlotZeroBinding);

                    var clips = index.GetClipsForBinding(binding).ToList();
                    probe.ClipsForBindingInSecondWindow = clips.Count;
                    if (clips.Count == 1)
                    {
                        probe.SecondWindowObservedCurve = DescribeObjectCurve(
                            clips[0].GetObjectCurve(binding));
                    }

                    // The narrow, binding-scoped edit operation. It hands back
                    // only the clips that actually carry the binding.
                    index.EditClipsByBinding(new[] { binding }, clip =>
                    {
                        probe.EditedClipNames.Add(clip.Name);

                        var curve = clip.GetObjectCurve(binding);
                        if (curve == null) return;

                        for (var key = 0; key < curve.Length; key++)
                        {
                            var value = curve[key].value;
                            if (value == null) continue;

                            if (!(value is Material material) ||
                                PreparedMapping == null ||
                                !PreparedMapping.TryGetValue(
                                    material, out var mapped))
                            {
                                // The refusal rule under investigation: an
                                // unrecognized value must stop the rewrite
                                // rather than be passed through.
                                probe.EveryValueWasMapped = false;
                                return;
                            }

                            curve[key].value = mapped;
                            probe.MappedKeyframeCount++;
                        }

                        // Times and curve placement are never touched: only the
                        // value field of each existing keyframe is replaced.
                        clip.SetObjectCurve(binding, curve);
                    });
                }
                catch (Exception exception)
                {
                    probe.Failure ??= exception;
                }
            }
        }

        // --- Observation helpers -------------------------------------------

        /// <summary>
        /// Non-perturbing lifecycle check: <c>BuildContext.Extension&lt;T&gt;</c>
        /// only reads the active-extension set and throws when absent, so
        /// catching that throw observes without activating anything.
        /// </summary>
        private static bool IsAnimatorServicesContextActive(BuildContext context)
        {
            try
            {
                context.Extension<AnimatorServicesContext>();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string DescribeObjectCurve(ObjectReferenceKeyframe[] curve)
        {
            if (curve == null) return "<null>";

            return string.Join("|", curve.Select(key =>
                key.time.ToString("R") + "=>" +
                (key.value == null ? "null" : key.value.name)));
        }

        private static string DescribeCommittedSlotCurve(
            GameObject avatarRoot,
            string rendererPath)
        {
            var descriptions = new List<string>();
            foreach (var clip in CommittedClips(avatarRoot))
            {
                foreach (var binding in
                         AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (binding.path != rendererPath ||
                        binding.propertyName != SlotZeroBinding)
                    {
                        continue;
                    }

                    descriptions.Add(DescribeObjectCurve(
                        AnimationUtility.GetObjectReferenceCurve(clip, binding)));
                }
            }

            return string.Join(" && ", descriptions);
        }

        private static IEnumerable<AnimationClip> CommittedClips(
            GameObject avatarRoot)
        {
            var seen = new HashSet<AnimationClip>();
            foreach (var animator in
                     avatarRoot.GetComponentsInChildren<Animator>(true))
            {
                if (!(animator.runtimeAnimatorController is AnimatorController
                        controller))
                {
                    continue;
                }

                foreach (var layer in controller.layers)
                {
                    foreach (var child in layer.stateMachine.states)
                    {
                        if (child.state.motion is AnimationClip clip &&
                            seen.Add(clip))
                        {
                            yield return clip;
                        }
                    }
                }
            }
        }

        private static AnimationClip CommittedClipWithBinding(
            GameObject avatarRoot,
            string rendererPath)
        {
            return CommittedClips(avatarRoot).SingleOrDefault(clip =>
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(
                    binding => binding.path == rendererPath &&
                               binding.propertyName == SlotZeroBinding));
        }

        // --- Fixture ---------------------------------------------------------

        private sealed class Fixture : IDisposable
        {
            private readonly List<UnityEngine.Object> tracked =
                new List<UnityEngine.Object>();

            internal GameObject Root;
            internal AnimatorController SourceController;
            internal AnimationClip SwapClip;
            internal AnimationClip DecoyClip;
            internal Material AlphaA;
            internal Material AlphaB;
            internal Material OpaqueA;
            internal Material OpaqueB;

            /// <summary>The exact keyframe times authored on the swap curve.</summary>
            internal static readonly float[] Times = { 0f, 0.25f, 1.5f };

            internal T Track<T>(T value) where T : UnityEngine.Object
            {
                tracked.Add(value);
                return value;
            }

            internal string SourceCurveDescription;
            internal string DecoyCurveDescription;

            internal static Fixture Create()
            {
                var fixture = new Fixture();
                try
                {
                    fixture.Build();
                    return fixture;
                }
                catch
                {
                    fixture.Dispose();
                    throw;
                }
            }

            private void Build()
            {
                var shader = Shader.Find("Standard");
                AlphaA = Track(new Material(shader) { name = "alpha A" });
                AlphaB = Track(new Material(shader) { name = "alpha B" });
                OpaqueA = Track(new Material(shader) { name = "opaque A" });
                OpaqueB = Track(new Material(shader) { name = "opaque B" });

                Root = Track(new GameObject("AMUSE reactivation characterization"));

                var swapped = new GameObject(RendererPath);
                swapped.transform.SetParent(Root.transform, false);
                var renderer = swapped.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMaterials = new[] { AlphaA };

                var other = new GameObject(OtherRendererPath);
                other.transform.SetParent(Root.transform, false);
                var otherRenderer = other.AddComponent<SkinnedMeshRenderer>();
                otherRenderer.sharedMaterials = new[] { AlphaB };

                // The clip that carries the binding under test.
                SwapClip = Track(new AnimationClip { name = CollidingClipName });
                SetSwapCurve(
                    SwapClip, RendererPath, new[] { AlphaA, AlphaB, AlphaA });

                // A second clip with the SAME NAME, carrying the same property
                // name on a DIFFERENT renderer path. Any name-based association
                // would wrongly sweep this in.
                DecoyClip = Track(new AnimationClip { name = CollidingClipName });
                SetSwapCurve(
                    DecoyClip, OtherRendererPath, new[] { AlphaB, AlphaA, AlphaB });

                SourceCurveDescription = DescribeAuthoredCurve(
                    SwapClip, RendererPath);
                DecoyCurveDescription = DescribeAuthoredCurve(
                    DecoyClip, OtherRendererPath);

                SourceController =
                    Track(new AnimatorController { name = "source controller" });
                SourceController.AddLayer("swap layer");
                var stateMachine = SourceController.layers[0].stateMachine;
                stateMachine.AddState("swap").motion = SwapClip;
                stateMachine.AddState("decoy").motion = DecoyClip;

                var animator = Root.AddComponent<Animator>();
                animator.runtimeAnimatorController = SourceController;
            }

            private static void SetSwapCurve(
                AnimationClip clip,
                string path,
                IReadOnlyList<Material> values)
            {
                var keyframes = new ObjectReferenceKeyframe[Times.Length];
                for (var index = 0; index < Times.Length; index++)
                {
                    keyframes[index] = new ObjectReferenceKeyframe
                    {
                        time = Times[index],
                        value = values[index],
                    };
                }

                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        path, typeof(SkinnedMeshRenderer), SlotZeroBinding),
                    keyframes);
            }

            private static string DescribeAuthoredCurve(
                AnimationClip clip,
                string path)
            {
                return DescribeObjectCurve(AnimationUtility.GetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        path, typeof(SkinnedMeshRenderer), SlotZeroBinding)));
            }

            internal string CurrentSourceCurveDescription =>
                DescribeAuthoredCurve(SwapClip, RendererPath);

            internal string CurrentDecoyCurveDescription =>
                DescribeAuthoredCurve(DecoyClip, OtherRendererPath);

            public void Dispose()
            {
                for (var index = tracked.Count - 1; index >= 0; index--)
                {
                    if (tracked[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(tracked[index]);
                    }
                }

                tracked.Clear();
            }
        }

        private sealed class ReactivationPlatform : INDMFPlatformProvider
        {
            internal static readonly ReactivationPlatform Instance =
                new ReactivationPlatform();

            public string QualifiedName => ReactivationPlatformName;
            public string DisplayName => "AMUSE animator reactivation";
        }

        // --- The characterization -------------------------------------------

        [Test]
        public void ReactivationPluginIsConfinedToItsDedicatedPlatform()
        {
            var attribute = System.Reflection.CustomAttributeData
                .GetCustomAttributes(typeof(ReactivationCharacterizationPlugin))
                .Single(value => value.AttributeType == typeof(RunsOnPlatforms));
            var platforms = attribute.ConstructorArguments
                .SelectMany(argument => argument.Value is
                    IReadOnlyCollection<
                        System.Reflection.CustomAttributeTypedArgument> values
                    ? values
                    : new[] { argument })
                .Select(argument => argument.Value as string)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { ReactivationPlatformName }, platforms);
        }

        [Test]
        public void
            ReactivatedAnimatorServicesObservesTheCommittedGraphAndCommitsMappedObjectCurves()
        {
            using var armed = AmusePlatformFinishPluginTests.SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            using var fixture = Fixture.Create();
            using var mapping = new PreparedMappingScope(
                new Dictionary<Material, Material>
                {
                    { fixture.AlphaA, fixture.OpaqueA },
                    { fixture.AlphaB, fixture.OpaqueB },
                });

            var expectedAuthored =
                "0=>alpha A|0.25=>alpha B|1.5=>alpha A";
            Assert.That(fixture.SourceCurveDescription, Is.EqualTo(expectedAuthored),
                "fixture authoring did not produce the intended source curve");

            var context = AvatarProcessor.ProcessAvatar(
                fixture.Root, ReactivationPlatform.Instance);
            var probe = context.GetState<ReactivationProbe>();

            Assert.That(probe.Failure, Is.Null,
                "the characterization plugin threw: " + probe.Failure);

            // --- 1. The lifecycle actually occurred as declared ------------
            Assert.That(probe.FirstWindowRan, Is.True, "first window did not run");
            Assert.That(probe.ContextActiveInFirstWindow, Is.True,
                "WithRequiredExtension did not activate AnimatorServicesContext");
            Assert.That(probe.BarrierRan, Is.True, "barrier did not run");
            Assert.That(probe.ContextInactiveAtBarrier, Is.True,
                "the extension-free barrier still saw an active context, so NDMF " +
                "did not deactivate and commit between the two windows");
            Assert.That(probe.SecondWindowRan, Is.True,
                "LIFECYCLE GATE: the second animator-services window never ran");
            Assert.That(probe.ContextActiveInSecondWindow, Is.True,
                "LIFECYCLE GATE: AnimatorServicesContext was not reactivated for " +
                "the third pass");

            // --- 2. The barrier saw a real committed graph -----------------
            Assert.That(probe.BarrierCommittedCurve, Is.EqualTo(expectedAuthored),
                "the committed clip observed outside the context did not carry the " +
                "authored material-slot keyframes");

            // --- 3. The SECOND index observes that same committed curve ----
            Assert.That(probe.ClipsForBindingInSecondWindow, Is.EqualTo(1),
                "the reactivated AnimationIndex did not associate exactly one " +
                "virtual clip with the slot-0 binding");
            Assert.That(probe.SecondWindowObservedCurve, Is.EqualTo(expectedAuthored),
                "LIFECYCLE GATE: the reactivated AnimationIndex did not observe the " +
                "committed material-slot keyframes and their exact times");

            // --- 4. Association is by binding, never by clip name ----------
            Assert.That(probe.EditedClipNames.Count, Is.EqualTo(1),
                "EditClipsByBinding visited " + probe.EditedClipNames.Count +
                " clips; two clips share a name but only one carries the binding, " +
                "so any count above one means name-based association");
            Assert.That(probe.EveryValueWasMapped, Is.True,
                "a curve value was absent from the prepared mapping");
            Assert.That(probe.MappedKeyframeCount, Is.EqualTo(3),
                "not every keyframe value was mapped");

            // --- 5. The second commit carries the mapped values ------------
            var committed = CommittedClipWithBinding(fixture.Root, RendererPath);
            Assert.That(committed, Is.Not.Null,
                "no committed clip carried the slot-0 binding after the build");

            var committedCurve = DescribeObjectCurve(
                AnimationUtility.GetObjectReferenceCurve(
                    committed,
                    EditorCurveBinding.PPtrCurve(
                        RendererPath, typeof(SkinnedMeshRenderer),
                        SlotZeroBinding)));

            Assert.That(committedCurve,
                Is.EqualTo("0=>opaque A|0.25=>opaque B|1.5=>opaque A"),
                "LIFECYCLE GATE: the final committed clip did not carry the mapped " +
                "opaque materials with preserved keyframe times");

            // --- 6. Source assets are untouched ---------------------------
            Assert.That(fixture.CurrentSourceCurveDescription,
                Is.EqualTo(expectedAuthored),
                "MUTATION BOUNDARY: the source AnimationClip was modified");
            Assert.That(fixture.CurrentDecoyCurveDescription,
                Is.EqualTo(fixture.DecoyCurveDescription),
                "MUTATION BOUNDARY: the same-named decoy clip was modified");
            Assert.That(committed, Is.Not.SameAs(fixture.SwapClip),
                "MUTATION BOUNDARY: the committed clip is the source clip itself");
            Assert.That(fixture.SourceController.layers[0].stateMachine.states
                    .Any(child => ReferenceEquals(child.state.motion,
                        fixture.SwapClip)),
                Is.True,
                "MUTATION BOUNDARY: the source controller no longer references " +
                "its own source clip");
            Assert.That(fixture.AlphaA.name, Is.EqualTo("alpha A"),
                "MUTATION BOUNDARY: a source material was modified");
            Assert.That(fixture.OpaqueA.name, Is.EqualTo("opaque A"),
                "the mapped-to material was modified");

            TestContext.WriteLine("Authored curve:            " + expectedAuthored);
            TestContext.WriteLine("Committed at barrier:      " +
                                  probe.BarrierCommittedCurve);
            TestContext.WriteLine("Observed in second window: " +
                                  probe.SecondWindowObservedCurve);
            TestContext.WriteLine("Final committed curve:     " + committedCurve);
            TestContext.WriteLine("Clips edited by binding:   " +
                                  string.Join(", ", probe.EditedClipNames));
        }
    }
}
