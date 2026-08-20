using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Frontend selection, and an explicit record of its public-project limit.
    /// <para>
    /// The public development project installs neither Poiyomi nor lilToon, so
    /// no material here can pass either frontend's source attestation. These
    /// tests therefore exercise the real refusal path on real Unity objects and
    /// make no claim about vendor dispatch, which remains a production
    /// capability the public suite cannot observe.
    /// </para>
    /// </summary>
    public sealed class UnityMaterialSemanticsTests
    {
        private Material _material;

        [TearDown]
        public void TearDown()
        {
            if (_material != null)
            {
                Object.DestroyImmediate(_material);
            }

            _material = null;
        }

        private static void AssertAllUnknown(MaterialSemantics semantics)
        {
            Assert.That(semantics, Is.Not.Null);
            Assert.That(semantics.BaseColor.IsComplete, Is.False);
            Assert.That(semantics.Alpha.IsComplete, Is.False);
            Assert.That(semantics.Emission.IsComplete, Is.False);
            Assert.That(semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void NullMaterialIsAllUnknownRatherThanAThrow()
        {
            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(null));
        }

        [Test]
        public void DestroyedMaterialIsAllUnknown()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            Object.DestroyImmediate(material);

            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(material));
        }

        [Test]
        public void MaterialNeitherFrontendAttestsIsAllUnknown()
        {
            _material = new Material(Shader.Find("Unlit/Color"));

            AssertAllUnknown(
                UnityMaterialSemantics.AnalyzeBaseMaterial(_material));
        }

        [Test]
        public void AllUnknownIsUnknownInEveryOutput()
        {
            AssertAllUnknown(UnityMaterialSemantics.AllUnknown());
        }
    }
}
