using System;
using System.Reflection;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// Temporary AMUSE-DBG trace helper for the 2026-09-18 zero-proof
    /// investigation. Every line it writes carries the AMUSE-DBG tag.
    /// Removal is tracked by the investigation note. The type ships no
    /// product behavior and must not outlive the investigation.
    /// </summary>
    internal static class AmuseDbgTrace
    {
        internal static void Line(string message)
        {
            Debug.Log("[AMUSE-DBG] " + message);
        }

        /// <summary>
        /// Reads one instance member by name regardless of declared
        /// accessibility, so a temporary dump never throws on an internal
        /// property.
        /// </summary>
        internal static object Member(
            System.Type type, string name, object target)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;
            var property = type.GetProperty(name, flags);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            var field = type.GetField(name, flags);
            return field != null
                ? field.GetValue(target)
                : "<missing:" + name + ">";
        }

        /// <summary>
        /// Dumps one <see cref="AlphaResolution"/> by reflection: the same
        /// private fields the test-side probe dumps, so the two dumps stay
        /// comparable line for line.
        /// </summary>
        internal static void Resolution(object resolution, string path)
        {
            if (resolution == null)
            {
                Line(path + ": null");
                return;
            }

            const BindingFlags flags = BindingFlags.NonPublic
                | BindingFlags.Instance;
            var type = resolution.GetType();
            var isUniform = (bool)type
                .GetField("_isUniform", flags).GetValue(resolution);
            var uniformOutcome = type
                .GetField("_uniformOutcome", flags).GetValue(resolution);
            var isProduct = (bool)type
                .GetField("_isProduct", flags).GetValue(resolution);
            var isDisjunction = (bool)type
                .GetField("_isDisjunction", flags).GetValue(resolution);
            Line(path + ": isUniform=" + isUniform +
                 " outcome=" + uniformOutcome +
                 " product=" + isProduct +
                 " or=" + isDisjunction);
            var chain = type.GetField("_chain", flags).GetValue(resolution);
            if (chain is AlphaMipChain mipChain)
            {
                var levels = (Array)typeof(AlphaMipChain)
                    .GetField("_levels", flags).GetValue(mipChain);
                for (var index = 0; index < levels.Length; index++)
                {
                    var level = levels.GetValue(index);
                    if (level == null)
                    {
                        Line(path + ".chain[" + index + "]: null");
                        continue;
                    }

                    var levelType = level.GetType();
                    var alpha8 = (byte[])levelType
                        .GetField("_alpha8", flags).GetValue(level);
                    var exact = 0;
                    foreach (var b in alpha8)
                    {
                        if (b == 255)
                        {
                            exact++;
                        }
                    }

                    Line(path + ".chain[" + index + "] exact255=" +
                         exact + "/" + alpha8.Length);
                }
            }

            var mapping = type.GetField("_mapping", flags)
                .GetValue(resolution);
            if (mapping != null)
            {
                Line(path + ".mapping=" + mapping);
            }

            var first = type.GetField("_firstFactor", flags)
                .GetValue(resolution);
            var second = type.GetField("_secondFactor", flags)
                .GetValue(resolution);
            if (first != null)
            {
                Resolution(first, path + ".first");
            }

            if (second != null)
            {
                Resolution(second, path + ".second");
            }
        }
    }
}
