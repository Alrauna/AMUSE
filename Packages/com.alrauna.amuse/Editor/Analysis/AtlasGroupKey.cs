using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// The identity of one atlas group. Two uses join one group when
    /// their keys are equal, so every fact that changes the page or
    /// the page material of a use must enter the key. The material key
    /// already holds every serialized material fact of the slot, and
    /// the capture pinned the shader row. The route name separates
    /// future routes of one row, because each route builds its own
    /// pages.
    /// </summary>
    internal sealed class AtlasGroupKey : IEquatable<AtlasGroupKey>
    {
        /// <summary>
        /// The route name of the main color route. Milestone 2 plans
        /// only this route. A later row that adds a route names it here
        /// so its uses land in their own groups.
        /// </summary>
        internal const string MainColorRouteName = "PoiyomiMainColor";

        internal string ShaderRowName { get; }

        /// <summary>The route of the member texture inside the row.</summary>
        internal string RouteName { get; }

        /// <summary>
        /// The graphics format of the member sources. The page copies
        /// it, so uses of different formats cannot share a page.
        /// </summary>
        internal GraphicsFormat SourceFormat { get; }

        /// <summary>
        /// The sampler facts of the route. The page copies them, so a
        /// page never mixes samplers.
        /// </summary>
        internal AtlasSamplerFacts Sampler { get; }

        /// <summary>
        /// The deterministic material key of the slot material. It
        /// holds every non-texture material fact of the slot.
        /// </summary>
        internal string MaterialKey { get; }

        /// <summary>
        /// The animated property names of the renderer, distinct and
        /// sorted by ordinal. Sorted, because the capture observes the
        /// names in renderer order and the key must not depend on that
        /// order.
        /// </summary>
        internal IReadOnlyList<string> AnimatedPropertyNames { get; }

        internal AtlasGroupKey(
            string shaderRowName,
            string routeName,
            GraphicsFormat sourceFormat,
            AtlasSamplerFacts sampler,
            string materialKey,
            IReadOnlyList<string> animatedPropertyNames)
        {
            ShaderRowName = shaderRowName;
            RouteName = routeName;
            SourceFormat = sourceFormat;
            Sampler = sampler;
            MaterialKey = materialKey;
            var names = new List<string>(animatedPropertyNames);
            names.Sort(StringComparer.Ordinal);
            AnimatedPropertyNames = new ReadOnlyCollection<string>(names);
        }

        /// <summary>Builds the group key of one captured use.</summary>
        internal static AtlasGroupKey FromUse(CapturedAtlasUse use)
        {
            return new AtlasGroupKey(
                use.ShaderRowName,
                MainColorRouteName,
                use.SourceFormat,
                use.Sampler,
                use.MaterialKey,
                use.AnimatedPropertyNames);
        }

        public bool Equals(AtlasGroupKey other)
        {
            if (other is null)
            {
                return false;
            }

            if (!string.Equals(ShaderRowName, other.ShaderRowName, StringComparison.Ordinal) ||
                !string.Equals(RouteName, other.RouteName, StringComparison.Ordinal) ||
                SourceFormat != other.SourceFormat ||
                !Sampler.Equals(other.Sampler) ||
                !string.Equals(MaterialKey, other.MaterialKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (AnimatedPropertyNames.Count != other.AnimatedPropertyNames.Count)
            {
                return false;
            }

            for (var index = 0; index < AnimatedPropertyNames.Count; index++)
            {
                if (!string.Equals(
                        AnimatedPropertyNames[index],
                        other.AnimatedPropertyNames[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is AtlasGroupKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 397) ^
                        StringComparer.Ordinal.GetHashCode(ShaderRowName);
                hash = (hash * 397) ^
                        StringComparer.Ordinal.GetHashCode(RouteName);
                hash = (hash * 397) ^ (int)SourceFormat;
                hash = (hash * 397) ^ Sampler.GetHashCode();
                hash = (hash * 397) ^
                        StringComparer.Ordinal.GetHashCode(MaterialKey);
                foreach (var name in AnimatedPropertyNames)
                {
                    hash = (hash * 397) ^
                            StringComparer.Ordinal.GetHashCode(name);
                }

                return hash;
            }
        }
    }
}
