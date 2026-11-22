using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The atlas pass gates. Every test drives the real passes in the
    /// production order: the structural graph check, the barrier with
    /// exact supported lifecycle facts, then the atlas pass. The tests
    /// pin the build target through the shared test override, because
    /// the editor active build target differs between machines. The
    /// barrier owns every shared report, so the atlas pass may add
    /// only its own named refusal entries.
    /// </summary>
    public sealed class AtlasPassTests
    {
        private Func<BuildTarget> _originalTarget;

        [SetUp]
        public void PinWindowsBuildTarget()
        {
            _originalTarget =
                AmusePlatformFinishPlugin.ActiveBuildTargetForTests;
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                () => BuildTarget.StandaloneWindows64;
        }

        [TearDown]
        public void RestorePinnedBuildTarget()
        {
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                _originalTarget;
        }

        private static HostLifecycleFacts SupportedFacts(
            string unityVersion = "2022.3.22f1")
        {
            return new HostLifecycleFacts(
                unityVersion,
                "1.14.4",
                "3.10.4",
                "3.10.4",
                WellKnownPlatforms.VRChatAvatar30,
                AmuseBuildPath.NonPlayNdmfBuild,
                hasAssetSaver: true,
                hasAssetContainer: true,
                hasObjectRegistry: true,
                hasErrorReport: true);
        }

        /// <summary>
        /// Builds the atlas gate fixture: an opted-in avatar root with
        /// no renderer, so no alpha analysis can run and the only
        /// moving parts are the gates themselves.
        /// </summary>
        private static GameObject NewAtlasFixture(
            bool alphaSeparatorOn,
            bool atlasingOn,
            int? maximumPageSize,
            int? protectedMipLevelCount)
        {
            var root = new GameObject("AMUSE atlas gate fixture");
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            var component =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);

            if (!alphaSeparatorOn || !atlasingOn ||
                maximumPageSize.HasValue || protectedMipLevelCount.HasValue)
            {
                var serialized = new SerializedObject(component);
                if (!alphaSeparatorOn)
                {
                    var alphaSwitch =
                        serialized.FindProperty("_alphaSeparatorEnabled");
                    Assert.That(alphaSwitch, Is.Not.Null,
                        "AmuseAvatarOptimizer._alphaSeparatorEnabled field pin");
                    alphaSwitch.boolValue = false;
                }

                if (!atlasingOn)
                {
                    var atlasSwitch =
                        serialized.FindProperty("_atlasingEnabled");
                    Assert.That(atlasSwitch, Is.Not.Null,
                        "AmuseAvatarOptimizer._atlasingEnabled field pin");
                    atlasSwitch.boolValue = false;
                }

                if (maximumPageSize.HasValue)
                {
                    var pageSize =
                        serialized.FindProperty("_maximumAtlasPageSize");
                    Assert.That(pageSize, Is.Not.Null,
                        "AmuseAvatarOptimizer._maximumAtlasPageSize field pin");
                    pageSize.intValue = maximumPageSize.Value;
                }

                if (protectedMipLevelCount.HasValue)
                {
                    var protectedCount =
                        serialized.FindProperty("_protectedMipLevelCount");
                    Assert.That(protectedCount, Is.Not.Null,
                        "AmuseAvatarOptimizer._protectedMipLevelCount" +
                        " field pin");
                    protectedCount.intValue = protectedMipLevelCount.Value;
                }

                serialized.ApplyModifiedProperties();
            }

            return root;
        }

        /// <summary>
        /// The counting consent presenter. It records every call and
        /// the exact subjects it got, so a test can pin that the
        /// barrier asks at most once and with which subjects.
        /// </summary>
        private sealed class ConsentRecorder
        {
            internal bool Answer = true;

            internal int CallCount;

            internal readonly List<IReadOnlyList<string>> Presentations =
                new List<IReadOnlyList<string>>();

            internal bool Present(IReadOnlyList<string> subjects)
            {
                CallCount++;
                Presentations.Add(subjects);
                return Answer;
            }
        }

        /// <summary>
        /// Drives the three passes in the production order, exactly as
        /// the plugin registers them, and captures every report entry
        /// both passes produced.
        /// </summary>
        private static List<ErrorContext> DrivePasses(
            GameObject root,
            HostLifecycleFacts facts,
            VersionConsentPresenter presenter,
            out BuildContext context,
            out AmusePlatformFinishState state,
            Func<Material, bool> lockedOriginalAttestation = null)
        {
            context = AvatarProcessor.ProcessAvatar(
                root, AtlasTestPlatform.Instance);
            state = context.GetState<AmusePlatformFinishState>();
            state.AnimatorBindings =
                GenericPlatformAnimatorBindings.Instance;
            AmuseStructuralGraphCheck.Execute(context);

            // An out parameter cannot be captured, so the reports
            // close over this local instead.
            var capturedContext = context;
            var reports = new List<ErrorContext>();
            if (lockedOriginalAttestation == null)
            {
                reports.AddRange(ErrorReport.CaptureErrors(
                    () => AmusePlatformFinishPass.Execute(
                        capturedContext, facts, presenter)));
            }
            else
            {
                // The full entry with one stand-in seam: the window
                // fixture needs an attested original. The three middle
                // seams never run while alpha separation is off, so
                // their stubs only satisfy the entry contract.
                reports.AddRange(ErrorReport.CaptureErrors(
                    () => AmusePlatformFinishPass.Execute(
                        capturedContext,
                        facts,
                        delegate(
                            Material material,
                            out CapturedAlphaMaterialFamily family,
                            out MaterialEvidenceRequest alphaRelevance,
                            out MaterialEvidenceRequest captureRequest)
                        {
                            family = CapturedAlphaMaterialFamily
                                .Unsupported;
                            alphaRelevance = default;
                            captureRequest = default;
                            return false;
                        },
                        delegate(
                            IReadOnlyList<Material> materials,
                            IReadOnlyList<CapturedAlphaMaterialFamily>
                                families,
                            MaterialEvidenceRequest request,
                            AlphaPolicyBounds bounds,
                            int cutoutRawMipCap,
                            int exactRawMipCap,
                            out ClosedAlphaCaptureOutcome outcome)
                        {
                            outcome = default;
                            return false;
                        },
                        delegate(CapturedAlphaMaterial material)
                        {
                            return null;
                        },
                        consentPresenter: presenter,
                        lockedOriginalAttestation: lockedOriginalAttestation)));
            }

            reports.AddRange(ErrorReport.CaptureErrors(
                () => AtlasPass.Execute(capturedContext)));
            return reports;
        }

        private static Mesh TriangleMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        private const string AtlasConsentShaderFolder =
            "Assets/AmuseTests_AtlasConsent";

        /// <summary>
        /// An atlas fixture whose renderer assigns a material on a
        /// stand-in shader that carries a supported family's vendor
        /// name. The shader source is not a verified version, so the
        /// alpha-separation transfer scan raises one consent subject
        /// for it. The scope owns the shader asset and the test owns
        /// the mesh and the root.
        /// </summary>
        private static (GameObject root, TestTransientScope scope,
            Mesh mesh) NewTransferSubjectFixture()
        {
            var scope = new TestTransientScope(
                allowDestroyingAssets: true,
                tempFolder: AtlasConsentShaderFolder);
            scope.EnsureTempFolder();
            var shader = TestShaderWriter.WriteTestShader(
                AtlasConsentShaderFolder + "/atlas-consent-cutout.shader",
                "Shader \"" + LilToonSourceAttestation.CutoutShaderName +
                "\"\n{\n    Properties\n    {\n" +
                "        _MainTex (\"Main\", 2D) = \"white\" {}\n" +
                "        _Color (\"Color\", Color) = (1,1,1,1)\n" +
                "        _Cutoff (\"Cutoff\", Range(0, 1)) = 0.5\n" +
                "    }\n    SubShader { Pass {} }\n}\n");
            var material = scope.Track(new Material(shader));
            var mesh = TriangleMesh();
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { material };
            return (root, scope, mesh);
        }

        /// <summary>
        /// An atlas fixture whose renderer assigns a locked stand-in
        /// material with a recognized identity. The test owns the
        /// mesh, the root, and the locked source folder cleanup.
        /// </summary>
        private static (GameObject root, Mesh mesh)
            NewLockedMaterialFixture()
        {
            var locked = TransientUnlockTestLifecycle.LockedMaterial(
                "AMUSE atlas locked stand-in");
            var mesh = TriangleMesh();
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { locked };
            return (root, mesh);
        }

        [Test]
        public void AtlasOnlyBuildCollectsNoShaderTransferSubject()
        {
            var (root, scope, mesh) = NewTransferSubjectFixture();
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root,
                    SupportedFacts(unityVersion: "2022.3.23f1"),
                    recorder.Present,
                    out var context,
                    out var state);

                Assert.That(recorder.CallCount, Is.EqualTo(1),
                    "the one host subject must raise the shared dialog");
                Assert.That(recorder.Presentations[0],
                    Is.EqualTo(state.Lifecycle.ConsentSubjects),
                    "an atlas-only build must ask about the host " +
                    "versions only, because shader transfer subjects " +
                    "concern alpha separation alone");
                Assert.That(state.ReachedAtlasAnalysis, Is.True,
                    "the granted host consent lets the atlas pass run");
            }
            finally
            {
                scope.TearDown();
                UnityEngine.Object.DestroyImmediate(mesh, true);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AtlasOnlyBuildOpensNoWindowForALockedMaterial()
        {
            var (root, mesh) = NewLockedMaterialFixture();
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root,
                    SupportedFacts(),
                    recorder.Present,
                    out var context,
                    out var state,
                    lockedOriginalAttestation: material => true);

                var window = context.GetState<TransientUnlockWindowState>();
                Assert.That(window.ConsentGranted, Is.False,
                    "an atlas-only build must not open the unlock " +
                    "window, not even for a window-eligible locked " +
                    "material");
                Assert.That(window.OpenPairs, Is.Empty,
                    "an atlas-only build must run no swap-in");
                Assert.That(state.ReachedRendererAnalysis, Is.False,
                    "an atlas-only build must start no alpha work");
                Assert.That(state.ReachedAtlasAnalysis, Is.True,
                    "the atlas gates still run behind the shared verdict");
            }
            finally
            {
                LockedSourceFixture.DeleteFolder();
                UnityEngine.Object.DestroyImmediate(mesh, true);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int CountEntries(
            List<ErrorContext> reports, string titleKey)
        {
            var count = 0;
            foreach (var entry in reports)
            {
                if (entry.TheError is SimpleError simple &&
                    simple.TitleKey == titleKey)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountEntriesWithPrefix(
            List<ErrorContext> reports, string titleKeyPrefix)
        {
            var count = 0;
            foreach (var entry in reports)
            {
                if (entry.TheError is SimpleError simple &&
                    simple.TitleKey.StartsWith(titleKeyPrefix))
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void AtlasOnlyBuildOnWindowsReachesAtlasAnalysis()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                var reports = DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(state.ReachedAtlasAnalysis, Is.True,
                    "a verified Windows host with only atlasing on must" +
                    " reach the atlas analysis point");
                Assert.That(state.AtlasRefusal,
                    Is.EqualTo(AtlasUseRefusal.None));
                Assert.That(recorder.CallCount, Is.Zero,
                    "a verified host raises no consent ask");
                Assert.That(
                    CountEntriesWithPrefix(reports, "amuse.") -
                        CountEntriesWithPrefix(reports, "amuse.atlas.Summary"),
                    Is.Zero,
                    "an admitted atlas-only build reports nothing but" +
                    " its atlas summary");
                Assert.That(
                    CountEntriesWithPrefix(reports, "amuse.atlas.Summary"),
                    Is.EqualTo(1),
                    "a build that reaches the atlas analysis reports one" +
                    " atlas summary");
                Assert.That(state.AnalyzedRendererCount, Is.Zero,
                    "no renderer enters alpha analysis while alpha" +
                    " separation is off");
                Assert.That(state.AtlasPageCount, Is.EqualTo(0),
                    "an avatar with no renderer must build no atlas" +
                    " page");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(BuildTarget.StandaloneLinux64)]
        [TestCase(BuildTarget.Android)]
        public void NonWindowsTargetRefusesAtlasingWithOneNamedEntry(
            BuildTarget target)
        {
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                () => target;
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                var reports = DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(state.AtlasRefusal,
                    Is.EqualTo(AtlasUseRefusal.UnsupportedBuildTarget),
                    "a non-Windows target must refuse atlasing");
                Assert.That(
                    CountEntries(
                        reports,
                        AmuseReportStrings.AtlasKey(
                            AtlasUseRefusal.UnsupportedBuildTarget)),
                    Is.EqualTo(1),
                    "an unsupported target is one named console entry," +
                    " never two and never silent");
                Assert.That(state.ReachedAtlasAnalysis, Is.False,
                    "a refused target must not reach the atlas analysis" +
                    " point");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(8193)]
        [TestCase(0)]
        public void MaximumPageSizeOutsideTheRangeRefusesWithItsName(
            int pageSize)
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: pageSize,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                var reports = DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(state.AtlasRefusal,
                    Is.EqualTo(AtlasUseRefusal.MaximumPageSizeOutOfRange),
                    "a page size outside 1 to 8192 must refuse atlasing");
                Assert.That(
                    CountEntries(
                        reports,
                        AmuseReportStrings.AtlasKey(
                            AtlasUseRefusal.MaximumPageSizeOutOfRange)),
                    Is.EqualTo(1),
                    "an out-of-range page size is one named console" +
                    " entry");
                Assert.That(state.ReachedAtlasAnalysis, Is.False,
                    "a refused page size must not reach the atlas" +
                    " analysis point");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(8192)]
        [TestCase(1)]
        public void MaximumPageSizeAtTheLimitIsAccepted(int pageSize)
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: pageSize,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(state.ReachedAtlasAnalysis, Is.True,
                    "a page size of " + pageSize +
                    " is inside the valid range");
                Assert.That(state.AtlasRefusal,
                    Is.EqualTo(AtlasUseRefusal.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(0)]
        [TestCase(15)]
        public void ProtectedMipLevelCountOutsideTheRangeRefusesWithItsName(
            int protectedMipLevelCount)
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: protectedMipLevelCount);
            try
            {
                var recorder = new ConsentRecorder();
                var reports = DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(
                    state.AtlasRefusal,
                    Is.EqualTo(
                        AtlasUseRefusal.ProtectedMipLevelCountOutOfRange),
                    "a protected count outside 1 to 14 must refuse" +
                    " atlasing");
                Assert.That(
                    CountEntries(
                        reports,
                        AmuseReportStrings.AtlasKey(
                            AtlasUseRefusal
                                .ProtectedMipLevelCountOutOfRange)),
                    Is.EqualTo(1),
                    "an out-of-range protected count is one named" +
                    " console entry");
                Assert.That(state.ReachedAtlasAnalysis, Is.False,
                    "a refused protected count must not reach the atlas" +
                    " analysis point");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(1)]
        [TestCase(14)]
        public void ProtectedMipLevelCountInsideTheRangeIsAccepted(
            int protectedMipLevelCount)
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: protectedMipLevelCount);
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(state.ReachedAtlasAnalysis, Is.True,
                    "a protected count of " + protectedMipLevelCount +
                    " is inside the valid range");
                Assert.That(state.AtlasRefusal,
                    Is.EqualTo(AtlasUseRefusal.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AtlasOnlyUnverifiedHostShowsTheSharedDialogOnce()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root,
                    SupportedFacts(unityVersion: "2022.3.23f1"),
                    recorder.Present,
                    out var context,
                    out var state);

                Assert.That(recorder.CallCount, Is.EqualTo(1),
                    "an atlas-only build with one host consent subject" +
                    " asks exactly once");
                Assert.That(recorder.Presentations,
                    Has.Count.EqualTo(1),
                    "one ask means one recorded presentation");
                Assert.That(recorder.Presentations[0],
                    Is.EqualTo(state.Lifecycle.ConsentSubjects),
                    "the dialog must list the host subjects and only" +
                    " those, because shader transfer subjects concern" +
                    " alpha separation alone");
                Assert.That(state.ReachedAtlasAnalysis, Is.True,
                    "a granted consent lets the atlas pass run");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AtlasOnlyDeclinedConsentKeepsAtlasingIdle()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder
                {
                    Answer = false,
                };
                var reports = DrivePasses(
                    root,
                    SupportedFacts(unityVersion: "2022.3.23f1"),
                    recorder.Present,
                    out var context,
                    out var state);

                Assert.That(
                    CountEntries(reports, "amuse.consent.Declined"),
                    Is.EqualTo(1),
                    "a declined consent is one named console entry");
                Assert.That(state.SharedGatesPassed, Is.False,
                    "a declined consent must not pass the shared gates");
                Assert.That(state.ReachedAtlasAnalysis, Is.False,
                    "a declined consent keeps atlasing idle");
                Assert.That(
                    CountEntriesWithPrefix(reports, "amuse.atlas."),
                    Is.Zero,
                    "the atlas pass never repeats the barrier's report");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BothFeaturesShareOneDialog()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: true,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root,
                    SupportedFacts(unityVersion: "2022.3.23f1"),
                    recorder.Present,
                    out var context,
                    out var state);

                Assert.That(recorder.CallCount, Is.EqualTo(1),
                    "both features on share one dialog per build");
                Assert.That(state.ReachedAtlasAnalysis, Is.True,
                    "a granted shared consent lets the atlas pass run");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeclinedConsentKeepsAtlasingIdleAndSilent()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: true,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder
                {
                    Answer = false,
                };
                var reports = DrivePasses(
                    root,
                    SupportedFacts(unityVersion: "2022.3.23f1"),
                    recorder.Present,
                    out var context,
                    out var state);

                Assert.That(state.AtlasRefusal,
                    Is.EqualTo(AtlasUseRefusal.None),
                    "a declined consent is not an atlas refusal");
                Assert.That(state.ReachedAtlasAnalysis, Is.False,
                    "a declined consent keeps both features idle");
                Assert.That(
                    CountEntriesWithPrefix(reports, "amuse.atlas."),
                    Is.Zero,
                    "the atlas pass stays silent behind the barrier's" +
                    " own report");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AtlasOnlyBuildOpensNoUnlockWindow()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(
                    context.GetState<TransientUnlockWindowState>()
                        .ConsentGranted,
                    Is.False,
                    "an atlas-only build must not open the unlock" +
                    " window");
                Assert.That(state.ReachedRendererAnalysis, Is.False,
                    "an atlas-only build starts no alpha work after the" +
                    " shared gates");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LifecycleRefusalKeepsAtlasingSilent()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                var reports = DrivePasses(
                    root,
                    SupportedFacts(unityVersion: "2022.3.21f1"),
                    recorder.Present,
                    out var context,
                    out var state);

                Assert.That(CountEntriesWithPrefix(reports, "amuse.host."),
                    Is.EqualTo(1),
                    "the barrier owns the lifecycle refusal and reports" +
                    " it exactly once");
                Assert.That(
                    CountEntriesWithPrefix(reports, "amuse.atlas."),
                    Is.Zero,
                    "the atlas pass never repeats a barrier report");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BothSwitchesOffReportsNothing()
        {
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                () => BuildTarget.StandaloneLinux64;
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: false,
                maximumPageSize: 8193,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                var reports = DrivePasses(
                    root,
                    SupportedFacts(unityVersion: "2022.3.23f1"),
                    recorder.Present,
                    out var context,
                    out var state);

                Assert.That(recorder.CallCount, Is.Zero,
                    "both switches off must never ask for consent");
                Assert.That(CountEntriesWithPrefix(reports, "amuse."),
                    Is.Zero,
                    "both switches off means nothing is analyzed," +
                    " mutated, or reported");
                Assert.That(state.AtlasRefusal,
                    Is.EqualTo(AtlasUseRefusal.None),
                    "no gate ran, so no atlas refusal exists");
                Assert.That(state.ReachedAtlasAnalysis, Is.False,
                    "no gate ran, so the atlas analysis point stays" +
                    " unreached");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AtlasPassRunsOnlyOnce()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasFixture(
                alphaSeparatorOn: false,
                atlasingOn: true,
                maximumPageSize: null,
                protectedMipLevelCount: null);
            try
            {
                var recorder = new ConsentRecorder();
                DrivePasses(
                    root, SupportedFacts(), recorder.Present,
                    out var context, out var state);

                Assert.That(
                    () => AtlasPass.Execute(context),
                    Throws.InvalidOperationException,
                    "a second atlas pass run on the same build is a" +
                    " caller defect");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class AtlasTestPlatform : INDMFPlatformProvider
        {
            internal static readonly AtlasTestPlatform Instance =
                new AtlasTestPlatform();

            public string QualifiedName =>
                "com.alrauna.amuse.tests.atlas-pass";

            public string DisplayName =>
                "AMUSE atlas pass test generic";
        }
    }
}
