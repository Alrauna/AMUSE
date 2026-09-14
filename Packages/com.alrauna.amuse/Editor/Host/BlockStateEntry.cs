using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// One schema-probed block entry in the block's own storage: the slot it
    /// applies to, the declared property it rides, and the effective value
    /// after per-index-over-renderer-wide precedence. Texture entries carry
    /// the texture reference, so comparing two snapshots is exact for every
    /// value the materialization could copy.
    /// </summary>
    internal readonly struct BlockStateEntry
    {
        internal BlockStateEntry(
            int slotIndex,
            string name,
            ShaderPropertyType type,
            float floatValue,
            Color colorValue,
            Vector4 vectorValue,
            Texture textureValue)
        {
            SlotIndex = slotIndex;
            Name = name;
            Type = type;
            FloatValue = floatValue;
            ColorValue = colorValue;
            VectorValue = vectorValue;
            TextureValue = textureValue;
        }

        internal int SlotIndex { get; }
        internal string Name { get; }
        internal ShaderPropertyType Type { get; }
        internal float FloatValue { get; }
        internal Color ColorValue { get; }
        internal Vector4 VectorValue { get; }
        internal Texture TextureValue { get; }

        public bool Equals(BlockStateEntry other) =>
            SlotIndex == other.SlotIndex &&
            string.Equals(Name, other.Name, StringComparison.Ordinal) &&
            Type == other.Type &&
            FloatValue.Equals(other.FloatValue) &&
            ColorValue.Equals(other.ColorValue) &&
            VectorValue.Equals(other.VectorValue) &&
            ReferenceEquals(TextureValue, other.TextureValue);
    }
}
