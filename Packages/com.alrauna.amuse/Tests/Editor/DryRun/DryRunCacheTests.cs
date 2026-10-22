using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Tests.Editor.DryRun
{
    /// <summary>
    /// The fingerprint cache is the dry run's staleness defense. It is
    /// pure, so every sentence here runs without a scene. The one
    /// exception builds the dry-run policy fingerprint from a
    /// throwaway component. Each test pins one sentence the spec
    /// states about keys, eviction, and errored marks. The falsifiers
    /// each name one way a weaker key would serve stale rows to the
    /// user.
    /// </summary>
    public sealed class DryRunCacheTests
    {
        [SetUp]
        public void StartClean()
        {
            DryRunCache.Clear();
        }

        private static DryRunFingerprintInputs Inputs(
            int avatarRootInstanceId = 100,
            BuildTarget activeTarget = BuildTarget.StandaloneWindows64,
            string policy = "policy a=1 b=2 c=3 d=4 e=5 f=6 g=7 h=8 i=9 j=10 k=11 l=12 m=13",
            string controller = "controller guid-1 ticks-1",
            string graph = "graph layer-0 state-1")
        {
            return new DryRunFingerprintInputs(
                avatarRootInstanceId,
                activeTarget,
                policy,
                controller,
                graph);
        }

        private static List<MaterialTransformationRow> RowsOne()
        {
            return new List<MaterialTransformationRow>
            {
                MaterialTransformationRow.Banner("rows one"),
            };
        }

        private static List<MaterialTransformationRow> RowsTwo()
        {
            return new List<MaterialTransformationRow>
            {
                MaterialTransformationRow.Banner("rows two"),
            };
        }

        [Test]
        public void AvatarSwapEvictsPreviousAvatarRows()
        {
            // --- Falsifier 4: cross-avatar serve ---
            // A cache keyed by renderer fingerprint alone serves avatar
            // two the rows stored for avatar one, because both avatars
            // share the same renderer body here.
            var avatarOneInputs = Inputs();
            var avatarOneKey = DryRunFingerprint.Avatar(avatarOneInputs);
            var rendererKey = "renderer body";
            var rowsOne = RowsOne();
            DryRunCache.StoreRenderer(avatarOneKey, rendererKey, rowsOne);

            // Guard: a same-fingerprint re-lookup hits, so the miss
            // below proves the avatar swap and not a dead store path.
            Assert.That(DryRunCache.TryGetRenderer(
                DryRunFingerprint.Composite(avatarOneKey, rendererKey),
                out var guarded), Is.True);
            Assert.That(guarded, Is.SameAs(rowsOne));

            var avatarTwoInputs = Inputs(avatarRootInstanceId: 200);
            var avatarTwoKey = DryRunFingerprint.Avatar(avatarTwoInputs);
            Assert.That(avatarTwoKey, Is.Not.EqualTo(avatarOneKey));
            Assert.That(DryRunCache.TryGetRenderer(
                DryRunFingerprint.Composite(avatarTwoKey, rendererKey),
                out _), Is.False);
        }

        [Test]
        public void PolicyChangeInvalidatesRendererRows()
        {
            // --- Falsifier 5: stale policy rows ---
            // A cache keyed without the policy fingerprint serves rows
            // computed under the old slider after the user moves it.
            var beforeInputs = Inputs();
            var beforeAvatarKey = DryRunFingerprint.Avatar(beforeInputs);
            var rendererKey = "renderer body";
            DryRunCache.StoreRenderer(beforeAvatarKey, rendererKey, RowsOne());

            // Guard: the unchanged composite hits, so the miss below
            // proves the policy change and not a dead store path.
            Assert.That(DryRunCache.TryGetRenderer(
                DryRunFingerprint.Composite(beforeAvatarKey, rendererKey),
                out _), Is.True);

            var afterInputs = Inputs(
                policy: "policy a=9 b=2 c=3 d=4 e=5 f=6 g=7 h=8 i=9 j=10 k=11 l=12 m=13");
            var afterAvatarKey = DryRunFingerprint.Avatar(afterInputs);
            Assert.That(afterAvatarKey, Is.Not.EqualTo(beforeAvatarKey));
            Assert.That(DryRunCache.TryGetRenderer(
                DryRunFingerprint.Composite(afterAvatarKey, rendererKey),
                out _), Is.False);
        }

        [Test]
        public void FingerprintChangesWhenACutoutValueChanges()
        {
            // --- Falsifier 5: A fingerprint that omits the cutout
            // values leaves the dry-run view stale after a cutout edit.
            // The fingerprint-change assertion fails it. ---
            var before = DryRunPolicyFingerprint(cutoutMipLevel: 5);

            // Guard: a transparent edit moves the fingerprint, so the
            // miss below proves the cutout omission and not a dead
            // builder path.
            var transparentEdit =
                DryRunPolicyFingerprint(cutoutMipLevel: 5, transparentMipLevel: 6);
            Assert.That(transparentEdit, Is.Not.EqualTo(before));

            var after = DryRunPolicyFingerprint(cutoutMipLevel: 6);
            Assert.That(after, Is.Not.EqualTo(before));

            // A separator sub-switch is a policy value too, so turning
            // the cutout separator off moves the fingerprint.
            var separatorOff = DryRunPolicyFingerprint(
                cutoutMipLevel: 6, cutoutSeparatorEnabled: false);
            Assert.That(separatorOff, Is.Not.EqualTo(after));
        }

        /// <summary>
        /// Builds the policy fingerprint from a throwaway component
        /// through the shared store formatter. The component carries
        /// the stored cutout and transparent mip levels this file
        /// pins, plus the separator sub-switch state the caller asks
        /// for.
        /// </summary>
        private static string DryRunPolicyFingerprint(
            int cutoutMipLevel,
            int transparentMipLevel = 5,
            bool cutoutSeparatorEnabled = true)
        {
            var root = new GameObject("AMUSE dry run fingerprint fixture");
            try
            {
                var component = root.AddComponent<AmuseAvatarOptimizer>();

                // A first serialization round trip runs the one-time
                // upgrade backfill. The marker is then set, so the
                // cutout edit below keeps its stored value.
                var primed = EditorJsonUtility.ToJson(component);
                EditorJsonUtility.FromJsonOverwrite(primed, component);

                var serialized = new SerializedObject(component);
                serialized.FindProperty("_preserveTransparencyMaxMipLevel")
                    .intValue = transparentMipLevel;
                serialized.FindProperty("_cutoutPreserveTransparencyMaxMipLevel")
                    .intValue = cutoutMipLevel;
                if (!cutoutSeparatorEnabled)
                {
                    serialized.FindProperty("_cutoutSeparatorEnabled")
                        .boolValue = false;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();

                return PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetChangeInvalidatesAvatarRows()
        {
            var beforeInputs = Inputs();
            var beforeAvatarKey = DryRunFingerprint.Avatar(beforeInputs);
            DryRunCache.StoreAvatar(beforeAvatarKey, RowsOne());

            var afterInputs = Inputs(activeTarget: BuildTarget.Android);
            var afterAvatarKey = DryRunFingerprint.Avatar(afterInputs);

            // A platform switch invalidates everything, so the avatar
            // key changes and the stored rows no longer resolve.
            Assert.That(afterAvatarKey, Is.Not.EqualTo(beforeAvatarKey));
            Assert.That(DryRunCache.TryGetAvatar(afterAvatarKey, out _), Is.False);
        }

        [Test]
        public void ErroredStateSurvivesUntilClear()
        {
            var compositeKey = "composite key";
            Assert.That(DryRunCache.IsErrored(compositeKey), Is.False);

            DryRunCache.MarkErrored(compositeKey);
            Assert.That(DryRunCache.IsErrored(compositeKey), Is.True);

            DryRunCache.Clear();
            Assert.That(DryRunCache.IsErrored(compositeKey), Is.False);
        }

        [Test]
        public void StoreRendererIsSingleSlotPerAvatar()
        {
            var avatarOneKey = DryRunFingerprint.Avatar(Inputs());
            var avatarTwoKey = DryRunFingerprint.Avatar(Inputs(avatarRootInstanceId: 200));
            var rendererOneKey = "renderer one";
            var rendererTwoKey = "renderer two";
            DryRunCache.StoreAvatar(avatarOneKey, RowsOne());
            DryRunCache.StoreRenderer(avatarOneKey, rendererOneKey, RowsOne());

            DryRunCache.StoreAvatar(avatarTwoKey, RowsTwo());
            DryRunCache.StoreRenderer(avatarTwoKey, rendererTwoKey, RowsTwo());

            // Storing under avatar two removes avatar one's entries, so
            // the cache never holds two avatars at once.
            Assert.That(DryRunCache.TryGetAvatar(avatarOneKey, out _), Is.False);
            Assert.That(DryRunCache.TryGetAvatar(avatarTwoKey, out _), Is.True);
            Assert.That(DryRunCache.TryGetRenderer(
                DryRunFingerprint.Composite(avatarOneKey, rendererOneKey),
                out _), Is.False);
            Assert.That(DryRunCache.TryGetRenderer(
                DryRunFingerprint.Composite(avatarTwoKey, rendererTwoKey),
                out _), Is.True);
        }
    }
}
