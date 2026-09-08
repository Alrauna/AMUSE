using System;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Describes whether a material executes alpha clipping (cutout) and its
    /// effective cutoff threshold.
    /// </summary>
    internal readonly struct MaterialCutoutSpecification : IEquatable<MaterialCutoutSpecification>
    {
        public bool IsCutout { get; }
        public float CutoffThreshold { get; }

        public MaterialCutoutSpecification(bool isCutout, float cutoffThreshold)
        {
            IsCutout = isCutout;
            CutoffThreshold = Mathf.Clamp01(cutoffThreshold);
        }

        public static MaterialCutoutSpecification OpaqueOrTransparent =>
            new(false, 1.0f);

        public static MaterialCutoutSpecification Cutout(float cutoffThreshold) =>
            new(true, cutoffThreshold);

        public bool Equals(MaterialCutoutSpecification other) =>
            IsCutout == other.IsCutout &&
            Mathf.Approximately(CutoffThreshold, other.CutoffThreshold);

        public override bool Equals(object obj) =>
            obj is MaterialCutoutSpecification other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(IsCutout, CutoffThreshold);
    }
}
