using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Human-readable descriptions of object-reference curves, shared by
    /// the alpha-separation suites and the animator reactivation
    /// characterization. Round-trip times keep full precision so a
    /// description pins exact keyframe values.
    /// </summary>
    internal static class CurveDescription
    {
        /// <summary>Describes raw object-reference keyframes.</summary>
        internal static string DescribeObjectCurve(
            ObjectReferenceKeyframe[] curve)
        {
            if (curve == null)
            {
                return "<null>";
            }

            return string.Join("|", curve.Select(key =>
                key.time.ToString("R") + "=>" +
                (key.value == null
                    ? "null"
                    : key.value.name)));
        }

        /// <summary>
        /// Describes the object-reference curve a clip carries for one
        /// skinned-mesh renderer slot binding.
        /// </summary>
        internal static string DescribeAuthoredCurve(
            AnimationClip clip,
            string rendererPath,
            string propertyName)
        {
            return DescribeObjectCurve(AnimationUtility.GetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    rendererPath, typeof(SkinnedMeshRenderer), propertyName)));
        }
    }
}
