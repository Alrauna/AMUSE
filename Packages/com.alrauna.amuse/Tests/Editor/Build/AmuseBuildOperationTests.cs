using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alrauna.Amuse.Editor.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Tests.Editor.Build.AmuseBuildOperationTests.AfterAmuseOperationPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class AmuseBuildOperationTests
    {
        private const string GeneratedAssetRoot = "Assets/AMUSE-GeneratedAssetTests";

        /// <summary>
        /// The prefix NDMF's ErrorReport.ReportError writes to the console for
        /// every recorded error that is not a StackTraceError.
        /// </summary>
        private const string NdmfReportedErrorPrefix = "[NDMF] Error Reported: ";

        [Test]
        public void PreparationCompletesBeforeFirstMutation()
        {
            var events = new List<string>();
            var result = AmuseBuildOperation.Execute(
                SupportedCapability(),
                new RecordingAssetSaver(events),
                saver =>
                {
                    events.Add("prepare");
                    return AmusePreparationDecision.Ready();
                },
                () => events.Add("mutate"));

            Assert.That(events, Is.EqualTo(new[] { "prepare", "mutate" }));
            Assert.That(result.Outcome, Is.EqualTo(AmuseBuildOperationOutcome.Mutated));
        }

        [Test]
        public void LifecycleRefusalNeverInvokesMutation()
        {
            var mutated = false;
            var result = AmuseBuildOperation.Execute(
                RefusedCapability(),
                new RecordingAssetSaver(),
                _ => AmusePreparationDecision.Ready(),
                () => mutated = true);
            Assert.That(result.Outcome, Is.EqualTo(AmuseBuildOperationOutcome.LifecycleRefused));
            Assert.That(mutated, Is.False);
            Assert.That(
                result.Lifecycle.Refusal,
                Is.EqualTo(HostLifecycleRefusal.UnsupportedBuildPath));
        }

        [Test]
        public void ExplicitPreparationRefusalIsOrdinaryAndNeverInvokesApply()
        {
            var applied = false;
            var result = AmuseBuildOperation.Execute(
                SupportedCapability(),
                new RecordingAssetSaver(),
                _ => AmusePreparationDecision.Refused("unsupported synthetic input"),
                () => applied = true);

            Assert.That(
                result.Outcome,
                Is.EqualTo(AmuseBuildOperationOutcome.PreparationRefused));
            Assert.That(result.RefusalReason, Is.EqualTo("unsupported synthetic input"));
            Assert.That(applied, Is.False);
        }

        [Test]
        public void UnexpectedPreparationExceptionPropagatesWithoutApplying()
        {
            var applied = false;
            var exception = Assert.Throws<InvalidOperationException>(() =>
                AmuseBuildOperation.Execute(
                    SupportedCapability(),
                    new RecordingAssetSaver(),
                    _ => throw new InvalidOperationException(
                        "synthetic preparation failure"),
                    () => applied = true));

            Assert.That(exception.Message, Is.EqualTo("synthetic preparation failure"));
            Assert.That(applied, Is.False);
        }

        [Test]
        public void UnexpectedApplyExceptionPropagatesAfterFirstMutation()
        {
            var events = new List<string>();
            var exception = Assert.Throws<InvalidOperationException>(() =>
                AmuseBuildOperation.Execute(
                    SupportedCapability(),
                    new RecordingAssetSaver(events),
                    _ =>
                    {
                        events.Add("prepare");
                        return AmusePreparationDecision.Ready();
                    },
                    () =>
                    {
                        events.Add("mutate");
                        throw new InvalidOperationException(
                            "synthetic post-mutation failure");
                    }));

            Assert.That(exception.Message, Is.EqualTo("synthetic post-mutation failure"));
            Assert.That(events, Is.EqualTo(new[] { "prepare", "mutate" }));
        }

        [Test]
        public void NoMutationDecisionNeverInvokesApply()
        {
            var applied = false;
            var result = AmuseBuildOperation.Execute(
                SupportedCapability(),
                new RecordingAssetSaver(),
                _ => AmusePreparationDecision.NoMutation(),
                () => applied = true);

            Assert.That(
                result.Outcome,
                Is.EqualTo(AmuseBuildOperationOutcome.NoMutationRequired));
            Assert.That(result.RefusalReason, Is.Null);
            Assert.That(applied, Is.False);
        }

        [Test]
        public void PreparationReceivesTheSuppliedAssetSaver()
        {
            var saver = new RecordingAssetSaver();
            IAssetSaver observed = null;

            AmuseBuildOperation.Execute(
                SupportedCapability(),
                saver,
                actual =>
                {
                    observed = actual;
                    return AmusePreparationDecision.NoMutation();
                },
                () => { });

            Assert.That(observed, Is.SameAs(saver));
        }

        [Test]
        public void MissingArgumentsAreRejected()
        {
            var saver = new RecordingAssetSaver();
            PrepareAmuseMutation prepare = _ => AmusePreparationDecision.NoMutation();
            ApplyAmuseMutation apply = () => { };

            Assert.Throws<ArgumentNullException>(() =>
                AmuseBuildOperation.Execute(null, saver, prepare, apply));
            Assert.Throws<ArgumentNullException>(() =>
                AmuseBuildOperation.Execute(SupportedCapability(), null, prepare, apply));
            Assert.Throws<ArgumentNullException>(() =>
                AmuseBuildOperation.Execute(SupportedCapability(), saver, null, apply));
            Assert.Throws<ArgumentNullException>(() =>
                AmuseBuildOperation.Execute(SupportedCapability(), saver, prepare, null));
        }

        [Test]
        public void PreparationRefusalRequiresAReason()
        {
            Assert.Throws<ArgumentException>(
                () => AmusePreparationDecision.Refused(null));
            Assert.Throws<ArgumentException>(
                () => AmusePreparationDecision.Refused(string.Empty));
        }

        [Test]
        public void GeneratedMeshIsOwnedByTheActiveNdmfAssetSaver()
        {
            using var operation = OperationScope.Arm(OperationMode.GeneratedAsset);
            using var directory =
                new OverrideTemporaryDirectoryScope(GeneratedAssetRoot);
            var root = new GameObject("AMUSE generated asset fixture");
            var filter = root.AddComponent<MeshFilter>();
            root.AddComponent<MeshRenderer>();
            string generatedPath = null;

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);

                Assert.That(context.Successful, Is.True);
                Assert.That(operation.PrepareInvoked, Is.True);
                Assert.That(
                    operation.Result.Outcome,
                    Is.EqualTo(AmuseBuildOperationOutcome.Mutated));
                Assert.That(operation.SavedByActiveSaver, Is.True);

                var mesh = operation.GeneratedMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(EditorUtility.IsPersistent(mesh), Is.True);
                Assert.That(AssetDatabase.Contains(mesh), Is.True);

                generatedPath = AssetDatabase.GetAssetPath(mesh).Replace('\\', '/');
                Assert.That(generatedPath, Is.Not.Empty);
                Assert.That(generatedPath, Does.StartWith(GeneratedAssetRoot + "/"));
                Assert.That(generatedPath, Does.EndWith(".asset"));
                Assert.That(filter.sharedMesh, Is.SameAs(mesh));
            }
            finally
            {
                Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(GeneratedAssetRoot);
                Assert.That(
                    AssetDatabase.IsValidFolder(GeneratedAssetRoot), Is.False);
                if (!string.IsNullOrEmpty(generatedPath))
                {
                    Assert.That(
                        AssetDatabase.LoadMainAssetAtPath(generatedPath), Is.Null);
                }
            }
        }

        [Test]
        public void UnexpectedPreparationFailureBlocksTheBuildBeforeApply()
        {
            using var operation = OperationScope.Arm(OperationMode.PreparationFailure);
            using var directory = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE preparation failure fixture");
            var filter = root.AddComponent<MeshFilter>();
            root.AddComponent<MeshRenderer>();
            var sourceMesh = NewTriangleMesh();
            filter.sharedMesh = sourceMesh;
            ExpectReportedException("synthetic preparation failure");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);

                Assert.That(operation.PrepareInvoked, Is.True);
                Assert.That(operation.ApplyInvoked, Is.False);
                Assert.That(filter.sharedMesh, Is.SameAs(sourceMesh));
                Assert.That(context.Successful, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceMesh);
            }
        }

        [Test]
        public void FatalApplyFailureBlocksTheBuildAfterTheFirstMutation()
        {
            using var operation = OperationScope.Arm(OperationMode.FatalApply);
            using var directory = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE fatal apply fixture");
            var filter = root.AddComponent<MeshFilter>();
            root.AddComponent<MeshRenderer>();
            var sourceMesh = NewTriangleMesh();
            filter.sharedMesh = sourceMesh;
            ExpectReportedException("synthetic post-mutation failure");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);

                Assert.That(operation.ApplyInvoked, Is.True);
                Assert.That(filter.sharedMesh, Is.SameAs(operation.GeneratedMesh));
                Assert.That(context.Successful, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceMesh);
                Object.DestroyImmediate(operation.GeneratedMesh);
            }
        }

        [Test]
        public void ExplicitPreparationRefusalPreservesTheAvatarWithoutError()
        {
            using var operation = OperationScope.Arm(OperationMode.Refusal);
            using var directory = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE preparation refusal fixture");
            var filter = root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();
            var sourceMesh = NewTriangleMesh();
            var sourceMaterial = new Material(Shader.Find("Unlit/Color"))
            {
                color = Color.green,
            };
            filter.sharedMesh = sourceMesh;
            renderer.sharedMaterials = new[] { sourceMaterial };

            var reported = new List<string>();
            Application.LogCallback record = (condition, stackTrace, type) =>
            {
                if (type == LogType.Exception || condition.StartsWith(
                        NdmfReportedErrorPrefix, StringComparison.Ordinal))
                {
                    reported.Add(type + ": " + condition);
                }
            };

            Application.logMessageReceived += record;
            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);

                Assert.That(
                    operation.Result.Outcome,
                    Is.EqualTo(AmuseBuildOperationOutcome.PreparationRefused));
                Assert.That(
                    operation.Result.RefusalReason,
                    Is.EqualTo("unsupported synthetic input"));
                Assert.That(operation.ApplyInvoked, Is.False);
                Assert.That(filter.sharedMesh, Is.SameAs(sourceMesh));
                Assert.That(renderer.sharedMaterial, Is.SameAs(sourceMaterial));
                Assert.That(sourceMesh.vertexCount, Is.EqualTo(3));
                Assert.That(sourceMaterial.color, Is.EqualTo(Color.green));

                // An explicit refusal must record nothing at all, not merely
                // nothing fatal. NDMF's ErrorReport.ReportError logs every error
                // it records - Debug.LogException for a StackTraceError, and a
                // "[NDMF] Error Reported: " warning for every other IError - so an
                // empty capture proves no entry of any severity was recorded
                // during this build. BuildContext.Successful alone would not:
                // it only trips at ErrorSeverity.Error and above, and a warning
                // never fails a Unity test on its own.
                Assert.That(
                    reported,
                    Is.Empty,
                    "an explicit preparation refusal must report no error, but " +
                    "NDMF recorded: " + string.Join(" || ", reported));
                Assert.That(context.Successful, Is.True);
            }
            finally
            {
                Application.logMessageReceived -= record;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceMesh);
                Object.DestroyImmediate(sourceMaterial);
            }
        }

        /// <summary>
        /// A standing guard for the asset-ownership boundary: generated output
        /// belongs to the NDMF asset saver, so no AMUSE Editor source may reach
        /// past it into the asset database itself. This runs with every suite, so
        /// a future change that adds custom persistence fails here rather than
        /// waiting for someone to repeat a one-off scan by hand.
        /// </summary>
        [Test]
        public void ProductionEditorCodePersistsOnlyThroughTheNdmfAssetSaver()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AmuseBuildOperation).Assembly);
            Assert.That(
                package,
                Is.Not.Null,
                "could not resolve the AMUSE package from its Editor assembly, so " +
                "the production source could not be scanned");

            var editorRoot = Path.Combine(package.resolvedPath, "Editor");
            Assert.That(
                Directory.Exists(editorRoot),
                Is.True,
                "expected AMUSE Editor sources at " + editorRoot);

            var sources = Directory.GetFiles(
                editorRoot, "*.cs", SearchOption.AllDirectories);
            Assert.That(
                sources,
                Is.Not.Empty,
                "scanned no AMUSE Editor source files under " + editorRoot +
                ", so this test proved nothing");

            var offenders = sources
                .Where(path =>
                {
                    var text = File.ReadAllText(path);
                    return text.Contains("AssetDatabase.CreateAsset") ||
                           text.Contains("AssetDatabase.AddObjectToAsset");
                })
                .Select(path => path
                    .Substring(package.resolvedPath.Length)
                    .Replace('\\', '/')
                    .TrimStart('/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.That(
                offenders,
                Is.Empty,
                "AMUSE production code must persist generated output only through " +
                "BuildContext.AssetSaver, but these files call the asset database " +
                "directly: " + string.Join(", ", offenders));
        }

        /// <summary>
        /// Requires that NDMF reported the named uncaught pass exception. NDMF
        /// writes an exception to the console only along its InternalError path:
        /// the pass plugin's unhandled-exception hook logs it once, and
        /// ErrorReport.ReportError logs it a second time while recording the
        /// InternalError-severity StackTraceError that makes the build
        /// unsuccessful. An unmatched expectation fails the test, so this is an
        /// assertion, not a suppression.
        /// </summary>
        /// <remarks>
        /// The recorded error list itself (BuildContext.ErrorReport.Errors) is an
        /// ImmutableList, and this test assembly cannot name that type: Unity only
        /// grants auto-referenced precompiled assemblies such as NDMF's bundled
        /// System.Collections.Immutable.dll to assembly definitions with
        /// "autoReferenced": true, which test assemblies are not.
        /// </remarks>
        private static void ExpectReportedException(string message)
        {
            var pattern = new Regex(Regex.Escape(message));
            LogAssert.Expect(LogType.Exception, pattern);
            LogAssert.Expect(LogType.Exception, pattern);
        }

        private static Mesh NewTriangleMesh()
        {
            var mesh = new Mesh { name = "AMUSE operation test mesh" };
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        private static HostLifecycleCapability SupportedCapability()
        {
            return HostLifecycleCapability.Evaluate(
                Facts(AmuseBuildPath.NonPlayNdmfBuild));
        }

        private static HostLifecycleCapability RefusedCapability()
        {
            return HostLifecycleCapability.Evaluate(
                Facts(AmuseBuildPath.ApplyOnPlay));
        }

        private static HostLifecycleFacts Facts(AmuseBuildPath buildPath)
        {
            return new HostLifecycleFacts(
                "2022.3.22f1",
                "1.14.4",
                "3.10.4",
                "3.10.4",
                WellKnownPlatforms.VRChatAvatar30,
                buildPath,
                hasAssetSaver: true,
                hasAssetContainer: true,
                hasObjectRegistry: true,
                hasErrorReport: true);
        }

        public sealed class AfterAmuseOperationPlugin : Plugin<AfterAmuseOperationPlugin>
        {
            protected override void Configure()
            {
                InPhase(BuildPhase.PlatformFinish)
                    .AfterPlugin("com.alrauna.amuse")
                    .Run("AMUSE test build operation", Execute);
            }

            private static void Execute(BuildContext context)
            {
                OperationScope.Active?.Run(context);
            }
        }

        private enum OperationMode
        {
            GeneratedAsset,
            PreparationFailure,
            FatalApply,
            Refusal,
        }

        private sealed class OperationScope : IDisposable
        {
            private readonly OperationScope previous;
            private readonly OperationMode mode;

            private OperationScope(OperationMode mode)
            {
                this.mode = mode;
                previous = Active;
                Active = this;
            }

            internal static OperationScope Active { get; private set; }

            internal bool PrepareInvoked { get; private set; }
            internal bool ApplyInvoked { get; private set; }
            internal bool SavedByActiveSaver { get; private set; }
            internal Mesh GeneratedMesh { get; private set; }
            internal AmuseBuildOperationResult Result { get; private set; }

            internal static OperationScope Arm(OperationMode mode)
            {
                return new OperationScope(mode);
            }

            internal void Run(BuildContext context)
            {
                var filter = context.AvatarRootObject
                    .GetComponentInChildren<MeshFilter>(true);
                Mesh prepared = null;

                Result = AmuseBuildOperation.Execute(
                    SupportedCapability(),
                    context.AssetSaver,
                    saver =>
                    {
                        PrepareInvoked = true;
                        if (mode == OperationMode.PreparationFailure)
                        {
                            throw new InvalidOperationException(
                                "synthetic preparation failure");
                        }

                        if (mode == OperationMode.Refusal)
                        {
                            return AmusePreparationDecision.Refused(
                                "unsupported synthetic input");
                        }

                        prepared = NewTriangleMesh();
                        saver.SaveAsset(prepared);
                        SavedByActiveSaver =
                            saver.GetPersistedAssets().Contains(prepared);
                        GeneratedMesh = prepared;
                        return AmusePreparationDecision.Ready();
                    },
                    () =>
                    {
                        ApplyInvoked = true;
                        filter.sharedMesh = prepared;
                        if (mode == OperationMode.FatalApply)
                        {
                            throw new InvalidOperationException(
                                "synthetic post-mutation failure");
                        }
                    });
            }

            public void Dispose()
            {
                Active = previous;
            }
        }

        /// <summary>
        /// A saver double for the pure tests: it records what preparation handed
        /// it and never touches the asset database. The NDMF-owned saver is
        /// exercised by the integration tests instead.
        /// </summary>
        private sealed class RecordingAssetSaver : IAssetSaver
        {
            private readonly List<string> events;
            private readonly List<Object> saved = new List<Object>();

            internal RecordingAssetSaver()
                : this(null)
            {
            }

            internal RecordingAssetSaver(List<string> events)
            {
                this.events = events;
            }

            public Object CurrentContainer => null;

            public void SaveAsset(Object asset)
            {
                events?.Add("save");
                if (asset != null)
                {
                    saved.Add(asset);
                }
            }

            public bool IsTemporaryAsset(Object asset)
            {
                return !EditorUtility.IsPersistent(asset) || saved.Contains(asset);
            }

            public IEnumerable<Object> GetPersistedAssets()
            {
                return saved;
            }

            public void Dispose()
            {
            }
        }

        private sealed class TestVrchatPlatform : INDMFPlatformProvider
        {
            internal static readonly TestVrchatPlatform Instance =
                new TestVrchatPlatform();

            public string QualifiedName => WellKnownPlatforms.VRChatAvatar30;
            public string DisplayName => "AMUSE test VRChat";
        }
    }
}
