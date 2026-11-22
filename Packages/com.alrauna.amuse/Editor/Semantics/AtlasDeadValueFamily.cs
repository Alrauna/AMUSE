using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// One group of material properties that no pass of an admitted
    /// shader row reads while its switches hold the off value. A
    /// family never holds its own switch. The capture drops every
    /// member from the material key, so two materials whose only
    /// difference lies inside a dead family can share one page.
    /// <para>
    /// A switch that an animation clip or a property block drives
    /// keeps the whole family live. The runtime may hold any value
    /// then, so a dead value could change a pass.
    /// </para>
    /// </summary>
    internal sealed class AtlasDeadValueFamily
    {
        internal string Name { get; }

        /// <summary>
        /// The property names that decide the family. The list is
        /// empty when the shader row alone decides it.
        /// </summary>
        internal IReadOnlyList<string> Switches { get; }

        /// <summary>
        /// Whether every member of the family is dead. The delegate
        /// takes the material and the shader row name. A NaN switch
        /// counts as on, because the source compares with not equal
        /// to zero.
        /// </summary>
        internal Func<Material, string, bool> IsOff { get; }

        /// <summary>
        /// The exact member names, with no prefixes. A texture name
        /// also covers its saved scale and offset, because the key
        /// holds both in one token.
        /// </summary>
        internal IReadOnlyList<string> Properties { get; }

        internal AtlasDeadValueFamily(
            string name,
            string[] switches,
            Func<Material, string, bool> isOff,
            string[] properties)
        {
            Name = name;
            Switches = switches;
            IsOff = isOff;
            Properties = properties;
        }
    }
}
