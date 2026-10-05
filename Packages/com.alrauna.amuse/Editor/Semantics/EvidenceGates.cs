using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The evidence gates every material frontend reads the same way: the
    /// exact-off and binary-flag property gates over a live material or a
    /// captured evidence snapshot, the finiteness predicates those gates
    /// and every scalar comparison build on, the analyzability precondition,
    /// the lilToon unknown-recorder, and the all-Unknown sentinel no family
    /// selects. One definition, so two frontends can never disagree about
    /// what a proven-off property or a readable flag is.
    /// </summary>
    internal static class EvidenceGates
    {
        /// <summary>
        /// Returns the first property that fails the exact-off gate — missing,
        /// non-finite, or not exactly zero — or null when every property proves
        /// off. Naming the offending property lets an output diagnostic point at
        /// the exact enabled feature.
        /// </summary>
        internal static string FirstFailedZeroGate(
            Material material,
            params string[] properties)
        {
            foreach (var property in properties)
            {
                if (!material.HasProperty(property))
                {
                    return property;
                }

                var value = material.GetFloat(property);
                if (!IsFinite(value) || value != 0f)
                {
                    return property;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the first property that fails the exact-off gate — absent
        /// from the capture, non-finite, or not exactly zero — or null when
        /// every property proves off.
        /// </summary>
        internal static string FirstFailedZeroGate(
            CapturedMaterialEvidence evidence,
            params string[] properties)
        {
            foreach (var property in properties)
            {
                if (!evidence.TryGetScalar(property, out var value) ||
                    !IsFinite(value) || value != 0f)
                {
                    return property;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads a strictly binary (0 or 1) float flag. A missing, non-finite,
        /// or non-binary value cannot be read as a proven on/off state.
        /// </summary>
        internal static bool TryReadBinary(
            Material material,
            string property,
            out bool isSet)
        {
            isSet = false;
            if (!material.HasProperty(property))
            {
                return false;
            }

            var value = material.GetFloat(property);
            if (!IsFinite(value) || (value != 0f && value != 1f))
            {
                return false;
            }

            isSet = value == 1f;
            return true;
        }

        /// <summary>
        /// Reads a strictly binary (0 or 1) float flag off a captured
        /// evidence snapshot. An absent, non-finite, or non-binary capture
        /// cannot be read as a proven on/off state.
        /// </summary>
        internal static bool TryReadBinary(
            CapturedMaterialEvidence evidence,
            string property,
            out bool isSet)
        {
            isSet = false;
            if (!evidence.TryGetScalar(property, out var value) ||
                !IsFinite(value) || (value != 0f && value != 1f))
            {
                return false;
            }

            isSet = value == 1f;
            return true;
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        internal static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                   IsFinite(value.z) && IsFinite(value.w);
        }

        internal static void RequireAnalyzableMaterial(Material material)
        {
            if (ReferenceEquals(material, null))
            {
                throw new ArgumentNullException(nameof(material));
            }

            // Unity's overloaded equality reports a destroyed object as null.
            if (material == null)
            {
                throw new ArgumentException(
                    "The material has been destroyed and cannot be analyzed.",
                    nameof(material));
            }

            if (material.shader == null)
            {
                throw new ArgumentException(
                    "The material has no shader and cannot be analyzed.",
                    nameof(material));
            }
        }

        /// <summary>
        /// Records one lilToon diagnostic and answers the Unknown output of
        /// the requested kind. The Poiyomi frontend keeps its own analog,
        /// because it routes through the Poiyomi diagnostic vocabulary.
        /// </summary>
        internal static SemanticOutput<T> RecordUnknown<T>(
            List<LilToonSemanticDiagnostic> diagnostics,
            LilToonSemanticOutput output,
            LilToonSemanticDiagnosticCode code,
            string detail)
            where T : class
        {
            diagnostics.Add(new LilToonSemanticDiagnostic(output, code, detail));
            return SemanticOutput<T>.Unknown();
        }

        /// <summary>
        /// The admitted-material sentinel every output of which is Unknown.
        /// </summary>
        internal static MaterialSemantics AllUnknown()
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<ScalarSemanticValue>.Unknown(),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        /// <summary>
        /// Concatenates a target recipe schema and a family's source schema
        /// into the one fixed property order the finiteness sweep and the
        /// conversion reads index by. One definition, so the properties
        /// captured for a family and the properties its conversion decision
        /// reads cannot drift apart.
        /// </summary>
        internal static string[] BuildEligibilitySchema(
            IReadOnlyCollection<string> recipeSchema,
            string[] sourceSchema)
        {
            var schema = new string[recipeSchema.Count + sourceSchema.Length];
            var index = 0;
            foreach (var property in recipeSchema)
            {
                schema[index++] = property;
            }

            foreach (var property in sourceSchema)
            {
                schema[index++] = property;
            }

            return schema;
        }
    }
}
