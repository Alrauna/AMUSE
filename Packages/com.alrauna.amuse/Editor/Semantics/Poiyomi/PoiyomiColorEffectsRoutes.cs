using System;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The feature group 'Color, effects, and UV' of the reviewed Poiyomi 9.3
    /// route. The review record of 2026-11-23, sections 4.1, 4.3,
    /// and 6, carries the rows of this group. This file is a stub.
    /// A later task fills every member from the record.
    /// </summary>
    internal static class PoiyomiColorEffectsRoutes
    {
        internal static readonly string[] AllowedKeywords =
            Array.Empty<string>();

        internal static readonly string[] NeutralTextures =
            Array.Empty<string>();

        internal static readonly string[] ConditionProperties =
            Array.Empty<string>();

        internal static AtlasCompanionRoute[] Routes()
        {
            return Array.Empty<AtlasCompanionRoute>();
        }

        internal static string UnheldRequiredValue(Material material)
        {
            return null;
        }

        internal static AtlasDeadValueFamily[] Families()
        {
            return Array.Empty<AtlasDeadValueFamily>();
        }
    }
}
