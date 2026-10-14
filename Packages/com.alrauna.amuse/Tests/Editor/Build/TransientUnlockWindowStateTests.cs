using System;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The unlock window's per-build state. The load-bearing contract is
    /// the one-clone-per-locked-material rule: opening a pair for a
    /// material that already has one returns the open pair.
    /// </summary>
    public sealed class TransientUnlockWindowStateTests
    {
        [Test]
        public void OneClonePerLockedMaterialAcrossTheAvatar()
        {
            var window = new TransientUnlockWindowState();
            var first = Track(new Material(Shader.Find("Unlit/Color"))
            {
                name = "First",
            });
            var second = Track(new Material(Shader.Find("Unlit/Color"))
            {
                name = "Second",
            });

            var opened = window.OpenPair(first);

            Assert.That(window.OpenPair(first), Is.SameAs(opened),
                "a second open for one locked material returns the open " +
                "pair and never clones again");
            Assert.That(window.PairFor(first), Is.SameAs(opened));
            Assert.That(window.PairFor(second), Is.Null);

            var other = window.OpenPair(second);
            Assert.That(other, Is.Not.SameAs(opened),
                "a different locked material gets its own pair");
            Assert.That(window.OpenPairs.Count, Is.EqualTo(2));
        }

        [Test]
        public void OpenPairRejectsNullAndRemoveDrains()
        {
            var window = new TransientUnlockWindowState();
            Assert.Throws<ArgumentNullException>(
                () => window.OpenPair(null));

            var material = Track(new Material(Shader.Find("Unlit/Color"))
            {
                name = "Drained",
            });
            var pair = window.OpenPair(material);
            window.Remove(pair);
            Assert.That(window.OpenPairs, Is.Empty);
            Assert.That(window.PairFor(material), Is.Null);
            Assert.Throws<InvalidOperationException>(
                () => window.Remove(pair),
                "removing an absent pair is a caller defect and must " +
                "throw, because it means the close pass ran its " +
                "accounting twice");
        }

        [Test]
        public void ConsentGrantDefaultsToDenied()
        {
            var window = new TransientUnlockWindowState();
            Assert.That(window.ConsentGranted, Is.False,
                "the window is consent-gated per build from the first " +
                "commit, so the default is denied");
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            return _scope.Track(obj);
        }

        private readonly TestTransientScope _scope =
            new TestTransientScope();

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
        }
    }
}
