using System.IO;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The fixed session-cache reset list. The reset must drop the locked
    /// source alignment memo like every other session cache, because a
    /// stale aligned record would survive into the next build (A8.4).
    /// </summary>
    public sealed class AmuseSessionCachesTests
    {
        private readonly TestTransientScope _scope =
            new TestTransientScope(allowDestroyingAssets: true);

        [SetUp]
        public void SetUp()
        {
            LockedSourceFixture.DeleteFolder();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
            LockedSourceFixture.DeleteFolder();
            AssetDatabase.Refresh();
        }

        [Test]
        public void TheSessionCacheResetClearsTheLockedSourceAlignmentMemo()
        {
            var fixture = new LockedSourceFixture("caches");
            var locked = fixture.Locked("CacheCape");
            _scope.Track(locked);

            var originalText = LockedMaterialIdentity.ReadOriginalText(
                fixture.Original);
            Assert.That(originalText, Is.Not.Null,
                "fixture guard: the original text must be readable");
            var properties = LockedSourceAlignmentMemo.PropertiesOf(
                fixture.Original, originalText);

            // Prime the memo with the aligned pair.
            var primed = LockedSourceAlignmentMemo.GetOrAlign(
                fixture.Partner, fixture.Original, properties);
            Assert.That(primed.Succeeded, Is.True,
                "fixture guard: the pair must align before the reset, " +
                "but the aligner refused with " +
                (primed.RefusalDetail ?? "no sentence"));

            // The generated text changes while the write time stays
            // pinned. A stale memo would return the primed success.
            var generatedPath =
                AssetDatabase.GetAssetPath(fixture.Partner);
            var stamp = File.GetLastWriteTimeUtc(generatedPath);
            var broken = new LockedSourceFixture("caches");
            broken.WithGeneratedLine(
                "shader_master_label",
                "        shader_master_label " +
                "(\"Another Label\", Float) = 0");
            Assert.That(
                AssetDatabase.GetAssetPath(broken.Partner),
                Is.EqualTo(generatedPath),
                "fixture guard: the rewrite must target the same asset " +
                "path");
            File.SetLastWriteTimeUtc(generatedPath, stamp);

            AmuseSessionCaches.Clear();

            var cleared = LockedSourceAlignmentMemo.GetOrAlign(
                fixture.Partner, fixture.Original, properties);
            Assert.That(cleared.Succeeded, Is.False,
                "the reset must drop the memo, so the changed text " +
                "re-aligns");
            Assert.That(cleared.RefusalDetail,
                Is.EqualTo(
                    "A generated line does not match the expected " +
                    "line."),
                "the re-alignment must see the changed text");
        }
    }
}
