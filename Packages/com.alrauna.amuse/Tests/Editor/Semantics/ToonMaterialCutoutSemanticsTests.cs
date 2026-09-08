using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    [TestFixture]
    public sealed class ToonMaterialCutoutSemanticsTests
    {
        [Test]
        public void EvaluateCutoutSpecification_LilToonCutout_ExtractsValidCutoff()
        {
            var shader = Shader.Find("Hidden/lilToonCutout");
            if (shader == null)
            {
                // Stand-in or fallback shader for EditMode test
                shader = Shader.Find("Standard");
            }
            var mat = new Material(shader);
            try
            {
                mat.SetFloat("_Cutoff", 0.75f);
                var spec = ToonMaterialCutoutSemantics.EvaluateCutoutSpecification(
                    mat, CapturedAlphaMaterialFamily.LilToonCutout);

                Assert.That(spec.IsCutout, Is.True);
                Assert.That(spec.CutoffThreshold, Is.EqualTo(0.75f));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void EvaluateCutoutSpecification_LilToonCutout_RejectsExcessiveCutoff()
        {
            var shader = Shader.Find("Standard");
            var mat = new Material(shader);
            try
            {
                mat.SetFloat("_Cutoff", 1.05f); // > 0.9999f
                var spec = ToonMaterialCutoutSemantics.EvaluateCutoutSpecification(
                    mat, CapturedAlphaMaterialFamily.LilToonCutout);

                Assert.That(spec.IsCutout, Is.False);
                Assert.That(spec.CutoffThreshold, Is.EqualTo(1.0f));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void EvaluateCutoutSpecification_PoiyomiCutout_ExtractsValidCutoff()
        {
            var shader = Shader.Find("Standard");
            var mat = new Material(shader);
            try
            {
                mat.SetFloat("_Mode", 1f); // Cutout
                mat.SetFloat("_Cutoff", 0.6f);
                var spec = ToonMaterialCutoutSemantics.EvaluateCutoutSpecification(
                    mat, CapturedAlphaMaterialFamily.Poiyomi);

                Assert.That(spec.IsCutout, Is.True);
                Assert.That(spec.CutoffThreshold, Is.EqualTo(0.6f));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void EvaluateCutoutSpecification_PoiyomiForceOpaque_ReturnsOpaque()
        {
            var shader = Shader.Find("Standard");
            var mat = new Material(shader);
            try
            {
                mat.SetFloat("_Mode", 1f); // Cutout
                mat.SetFloat("_AlphaForceOpaque", 1f);
                mat.SetFloat("_Cutoff", 0.6f);
                var spec = ToonMaterialCutoutSemantics.EvaluateCutoutSpecification(
                    mat, CapturedAlphaMaterialFamily.Poiyomi);

                Assert.That(spec.IsCutout, Is.False);
                Assert.That(spec.CutoffThreshold, Is.EqualTo(1.0f));
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }
    }
}
