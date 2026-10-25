using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// One row of the original property table that the aligner reads
    /// (A3.1). The row carries the name, the type, the HDR and Gamma
    /// flags, and the float default. There is no texture default and no
    /// rule reads one. The row is plain data and never touches a Unity
    /// object.
    /// </summary>
    internal readonly struct LockedSourceProperty
    {
        internal LockedSourceProperty(
            string name,
            ShaderPropertyType type,
            bool hdr,
            bool gamma,
            float defaultFloat)
        {
            Name = name;
            Type = type;
            Hdr = hdr;
            Gamma = gamma;
            DefaultFloat = defaultFloat;
        }

        internal string Name { get; }

        internal ShaderPropertyType Type { get; }

        internal bool Hdr { get; }

        internal bool Gamma { get; }

        internal float DefaultFloat { get; }
    }
}
