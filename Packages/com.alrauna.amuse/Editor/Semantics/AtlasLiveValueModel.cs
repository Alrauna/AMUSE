using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The reviewed live value model of one admitted shader family.
    /// It groups the material properties into families that a switch
    /// or the shader row can turn fully dead, and it lists every
    /// uniform name the compiled shaders can read.
    /// <para>
    /// The capture consults the model when it builds the material
    /// key. A value that no pass can read must not split a group.
    /// A saved name that no shader declares, that is not a property
    /// of the row, and that no VRChat fallback shader reads is stale,
    /// so it must not split a group either.
    /// </para>
    /// </summary>
    internal sealed class AtlasLiveValueModel
    {
        internal IReadOnlyList<AtlasDeadValueFamily> Families { get; }

        /// <summary>
        /// Every uniform name the family declares, in every compile
        /// branch, plus every property name of the admitted rows. A
        /// saved value with one of these names may reach a shader, so
        /// it never counts as stale.
        /// </summary>
        internal IReadOnlyCollection<string> DeclaredUniforms =>
            declaredUniformNames;

        private readonly HashSet<string> declaredUniformNames;

        internal AtlasLiveValueModel(
            AtlasDeadValueFamily[] families,
            string[] declaredUniforms)
        {
            Families = families;
            declaredUniformNames = new HashSet<string>(declaredUniforms);
        }

        /// <summary>
        /// Whether the declared names hold one name. The lookups stay
        /// constant time.
        /// </summary>
        internal bool Declares(string name)
        {
            return declaredUniformNames.Contains(name);
        }

        /// <summary>
        /// The union of the member lists of every family that is off
        /// and that no dynamic switch guards. A switch counts as
        /// dynamic when the delegate accepts it. An empty switch
        /// list means the shader row alone decides the family, so no
        /// animation or property block can make it dynamic. A name a
        /// VRChat fallback shader can read never enters the result,
        /// because the fallback swap ignores the row. The result is
        /// fresh on every call, so a caller can never mutate the
        /// model.
        /// </summary>
        internal ISet<string> DeadProperties(
            Material material,
            string shaderRowName,
            Func<string, bool> switchIsDynamic)
        {
            var dead = new HashSet<string>();
            foreach (var family in Families)
            {
                if (!family.IsOff(material, shaderRowName))
                {
                    continue;
                }

                if (AnySwitchIsDynamic(family, switchIsDynamic))
                {
                    continue;
                }

                foreach (var property in family.Properties)
                {
                    dead.Add(property);
                }
            }

            dead.ExceptWith(FallbackNames);
            return dead;
        }

        private static readonly HashSet<string> FallbackNames =
            new HashSet<string>(VRChatFallbackProperties.Names);

        private static bool AnySwitchIsDynamic(
            AtlasDeadValueFamily family,
            Func<string, bool> switchIsDynamic)
        {
            foreach (var name in family.Switches)
            {
                if (switchIsDynamic(name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
