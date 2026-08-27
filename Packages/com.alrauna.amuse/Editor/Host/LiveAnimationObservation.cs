// TRANSIENT HOST OBSERVATION ONLY. These types intentionally may hold live
// UnityEngine.Object references while host evidence is being captured. Analysis
// and immutable captured-evidence types must never reference them; Task 10 is the
// one-way boundary that replaces every live reference with immutable evidence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal sealed class LiveFloatObservation
    {
        internal LiveFloatObservation(
            string path,
            string typeName,
            string propertyName,
            bool isFiniteExact,
            IList<float> values)
        {
            Path = path;
            TypeName = typeName;
            PropertyName = propertyName;
            IsFiniteExact = isFiniteExact;
            Values = new ReadOnlyCollection<float>(new List<float>(values));
        }

        internal string Path { get; }
        internal string TypeName { get; }
        internal string PropertyName { get; }
        internal bool IsFiniteExact { get; }
        internal IReadOnlyList<float> Values { get; }
    }

    internal sealed class LiveObjectObservation
    {
        internal LiveObjectObservation(
            string path,
            string typeName,
            string propertyName,
            IList<UnityEngine.Object> values)
        {
            Path = path;
            TypeName = typeName;
            PropertyName = propertyName;
            Values = new ReadOnlyCollection<UnityEngine.Object>(
                new List<UnityEngine.Object>(values));
        }

        internal string Path { get; }
        internal string TypeName { get; }
        internal string PropertyName { get; }
        internal IReadOnlyList<UnityEngine.Object> Values { get; }
    }

    internal sealed class LiveClipObservation
    {
        internal LiveClipObservation(
            string name,
            bool isSpecialMotion,
            IList<LiveFloatObservation> floats,
            IList<LiveObjectObservation> objects)
        {
            Name = name;
            IsSpecialMotion = isSpecialMotion;
            Floats = new ReadOnlyCollection<LiveFloatObservation>(
                new List<LiveFloatObservation>(floats));
            Objects = new ReadOnlyCollection<LiveObjectObservation>(
                new List<LiveObjectObservation>(objects));
        }

        internal string Name { get; }
        internal bool IsSpecialMotion { get; }
        internal IReadOnlyList<LiveFloatObservation> Floats { get; }
        internal IReadOnlyList<LiveObjectObservation> Objects { get; }
    }

    internal static class LiveAnimationObservation
    {
        private const string MaterialSlotPrefix = "m_Materials.Array.data[";

        internal static LiveClipObservation ObserveClip(
            AnimationClip clip,
            bool isSpecialMotion)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));

            var floats = new List<LiveFloatObservation>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var keys = curve.keys;
                var values = new float[keys.Length];
                for (var index = 0; index < keys.Length; index++)
                {
                    values[index] = keys[index].value;
                }

                floats.Add(new LiveFloatObservation(
                    binding.path,
                    binding.type.FullName,
                    binding.propertyName,
                    IsFiniteExact(keys),
                    values));
            }

            var objects = new List<LiveObjectObservation>();
            foreach (var binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                var values = new UnityEngine.Object[keys.Length];
                for (var index = 0; index < keys.Length; index++)
                {
                    values[index] = keys[index].value;
                }

                objects.Add(new LiveObjectObservation(
                    binding.path,
                    binding.type.FullName,
                    binding.propertyName,
                    values));
            }

            return new LiveClipObservation(
                clip.name, isSpecialMotion, floats, objects);
        }

        internal static bool TryParseMaterialSlotBinding(
            string propertyName,
            out int slotIndex)
        {
            slotIndex = default;
            if (propertyName == null ||
                !propertyName.StartsWith(
                    MaterialSlotPrefix, StringComparison.Ordinal) ||
                !propertyName.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }

            var indexLength = propertyName.Length - MaterialSlotPrefix.Length - 1;
            return indexLength > 0 && int.TryParse(
                propertyName.Substring(MaterialSlotPrefix.Length, indexLength),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out slotIndex);
        }

        private static bool IsFiniteExact(IReadOnlyList<Keyframe> keys)
        {
            if (keys.Count == 0) return false;

            foreach (var key in keys)
            {
                if (key.weightedMode != WeightedMode.None) return false;
            }

            for (var index = 0; index + 1 < keys.Count; index++)
            {
                var left = keys[index];
                var right = keys[index + 1];
                var stepped =
                    left.outTangent == float.PositiveInfinity &&
                    right.inTangent == float.PositiveInfinity;
                var exactlyFlat =
                    left.value == right.value &&
                    left.outTangent == 0f &&
                    right.inTangent == 0f;
                if (!stepped && !exactlyFlat) return false;
            }

            return true;
        }
    }
}
