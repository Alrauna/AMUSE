using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class AmuseAvatarOptimizerEditorTests
    {
        [Test]
        public void PolygonClampNormalizationKeepsTheInertSentinel()
        {
            // A drawn clamp of 100 is the inert sentinel. The inspector
            // must store it unchanged, because the build maps it to the
            // inert noise bound. Pushing it to opaque minus one would
            // bake an erased mask at the defaults and break the
            // byte-identity contract.
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(100, 100),
                Is.EqualTo(100));
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(50, 100),
                Is.EqualTo(100));

            // Below the sentinel the inspector clamp keeps the
            // tolerated band strictly below the opaque percent.
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(100, 95),
                Is.EqualTo(95));
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(50, 80),
                Is.EqualTo(49));

            // A stored opaque clamp at or below zero reads as the
            // inert 100, so the drawn per-polygon clamp keeps its
            // value instead of collapsing to zero.
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(0, 50),
                Is.EqualTo(50));
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(0, 100),
                Is.EqualTo(100));
        }
    }
}
