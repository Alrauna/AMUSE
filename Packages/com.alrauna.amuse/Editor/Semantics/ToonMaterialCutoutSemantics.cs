using System;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Extracts cutout threshold parameters across supported toon shader families
    /// (lilToon, Poiyomi, and future extensions).
    /// </summary>
    internal static class ToonMaterialCutoutSemantics
    {
        private const string CutoffProperty = "_Cutoff";
        private const string AlphaForceOpaqueProperty = "_AlphaForceOpaque";
        private const string ModeProperty = "_Mode";

        /// <summary>
        /// Evaluates the material's effective cutout specification.
        /// Returns Cutout(threshold) for cutout materials whose cutoff is provably valid,
        /// and OpaqueOrTransparent for opaque or blend materials.
        /// </summary>
        internal static MaterialCutoutSpecification EvaluateCutoutSpecification(
            Material material,
            CapturedAlphaMaterialFamily family)
        {
            if (material == null || material.shader == null)
            {
                return MaterialCutoutSpecification.OpaqueOrTransparent;
            }

            switch (family)
            {
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return EvaluateLilToonCutout(material);

                case CapturedAlphaMaterialFamily.Poiyomi:
                    return EvaluatePoiyomiCutout(material);

                default:
                    return MaterialCutoutSpecification.OpaqueOrTransparent;
            }
        }

        private static MaterialCutoutSpecification EvaluateLilToonCutout(Material material)
        {
            if (!material.HasProperty(CutoffProperty))
            {
                return MaterialCutoutSpecification.OpaqueOrTransparent;
            }

            var cutoff = material.GetFloat(CutoffProperty);
            if (!float.IsFinite(cutoff) || cutoff < 0f || cutoff > LilToonCutoutSourceEligibility.MaxProvableCutoff)
            {
                return MaterialCutoutSpecification.OpaqueOrTransparent;
            }

            return MaterialCutoutSpecification.Cutout(cutoff);
        }

        private static MaterialCutoutSpecification EvaluatePoiyomiCutout(Material material)
        {
            if (material.HasProperty(AlphaForceOpaqueProperty) &&
                material.GetFloat(AlphaForceOpaqueProperty) >= 0.5f)
            {
                return MaterialCutoutSpecification.OpaqueOrTransparent;
            }

            var isCutout = false;
            if (material.HasProperty(ModeProperty))
            {
                var mode = Mathf.RoundToInt(material.GetFloat(ModeProperty));
                isCutout = mode == 1 || mode == 9; // Cutout or TransClipping
            }
            else
            {
                var tag = material.GetTag("RenderType", false);
                isCutout = string.Equals(tag, "TransparentCutout", StringComparison.Ordinal);
            }

            if (!isCutout || !material.HasProperty(CutoffProperty))
            {
                return MaterialCutoutSpecification.OpaqueOrTransparent;
            }

            var cutoff = material.GetFloat(CutoffProperty);
            if (!float.IsFinite(cutoff) || cutoff < 0f || cutoff > 1.0f)
            {
                return MaterialCutoutSpecification.OpaqueOrTransparent;
            }

            return MaterialCutoutSpecification.Cutout(cutoff);
        }
    }
}
