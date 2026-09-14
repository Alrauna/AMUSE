using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// Why: a property block is part of the renderer's effective material
    /// state, so capture must prove the values the renderer actually shows,
    /// not the serialized values alone. For each slot the materializer clones
    /// the slot material when — and only when — a block entry for one of that
    /// shader's declared properties is present, and copies the entry onto the
    /// clone in Unity's precedence order: renderer-wide block first, then the
    /// per-material-index block.
    /// <para>
    /// The copy schema comes only from the shader's declared properties,
    /// enumerated through the Shader property API; properties are declared once
    /// per Shader, so the enumeration is complete. A block key that no shader
    /// property declares cannot change rendering, so leaving it uncopied is
    /// exact — the same rule covers matrix and buffer entries, which ShaderLab
    /// cannot declare at all. Declared texture properties are accompanied by
    /// their derived <c>_ST</c>, <c>_TexelSize</c>, and <c>_HDR</c> vectors;
    /// a block entry for any of them changes what the shader samples, so they
    /// belong to the schema.
    /// </para>
    /// <para>
    /// Originals are read-only inputs and are never mutated. The clones are
    /// transient capture inputs, never written back to a renderer or an asset:
    /// the caller destroys every entry of <paramref name="createdClones"/> in
    /// the same scope that received it, on every exit path.
    /// </para>
    /// </summary>
    internal static class EffectiveMaterialMaterialization
    {
        internal static Material[] Materialize(
            Renderer renderer,
            Material[] slotMaterials,
            out List<Material> createdClones,
            bool textureOnly = false)
        {
            createdClones = new List<Material>();
            if (!renderer.HasPropertyBlock())
            {
                return slotMaterials;
            }

            var wide = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(wide);
            var perIndex = new MaterialPropertyBlock[slotMaterials.Length];
            for (var index = 0; index < perIndex.Length; index++)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, index);
                perIndex[index] = block;
            }

            var schemas = new Dictionary<Shader, List<SchemaEntry>>();
            var result = new Material[slotMaterials.Length];
            var materializedAnySlot = false;
            for (var index = 0; index < slotMaterials.Length; index++)
            {
                var material = slotMaterials[index];
                var schema = SchemaFor(material, schemas);
                var touches = textureOnly
                    ? (TouchesTextureSchema(schema, wide) ||
                        TouchesTextureSchema(schema, perIndex[index]))
                    : (TouchesSchema(schema, wide) ||
                        TouchesSchema(schema, perIndex[index]));
                if (schema != null && touches)
                {
                    var clone = UnityEngine.Object.Instantiate(material);
                    createdClones.Add(clone);
                    ApplyBlock(clone, schema, wide);
                    ApplyBlock(clone, schema, perIndex[index]);
                    result[index] = clone;
                    materializedAnySlot = true;
                }
                else
                {
                    result[index] = material;
                }
            }

            return materializedAnySlot ? result : slotMaterials;
        }
        /// <summary>
        /// Materializes the effective state of admitted materials against one
        /// renderer: the renderer-wide block applies to every admitted
        /// material, and a per-material-index block applies when the shared
        /// materials hold the material in exactly one slot. A material held by
        /// several slots — or by none, a pure swap state — materializes from
        /// the renderer-wide block alone, because one material-scoped evidence
        /// record cannot carry slot disagreement; stage 2's per-property
        /// domains replace this scalar materialization and remove that corner.
        /// Clones are transient capture inputs: the caller destroys every
        /// entry of <paramref name="createdClones"/> in the receiving scope.
        /// </summary>
        internal static IReadOnlyList<Material> MaterializeAdmitted(
            Renderer renderer,
            IReadOnlyList<Material> admittedMaterials,
            out List<Material> createdClones)
        {
            createdClones = new List<Material>();
            if (admittedMaterials.Count == 0 || !renderer.HasPropertyBlock())
            {
                return admittedMaterials;
            }

            var wide = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(wide);
            var slotMaterials = renderer.sharedMaterials;
            var schemas = new Dictionary<Shader, List<SchemaEntry>>();
            var result = new Material[admittedMaterials.Count];
            var materializedAny = false;
            for (var index = 0; index < admittedMaterials.Count; index++)
            {
                var material = admittedMaterials[index];
                var schema = SchemaFor(material, schemas);
                var effective = material;
                if (schema != null)
                {
                    var slot = SingleOccupiedSlot(slotMaterials, material);
                    var wideTouches = TouchesTextureSchema(schema, wide);
                    var slotTouches = false;
                    MaterialPropertyBlock slotBlock = null;
                    if (slot >= 0)
                    {
                        slotBlock = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(slotBlock, slot);
                        slotTouches = TouchesTextureSchema(schema, slotBlock);
                    }

                    if (wideTouches || slotTouches)
                    {
                        effective = UnityEngine.Object.Instantiate(material);
                        createdClones.Add(effective);
                        if (wideTouches)
                        {
                            ApplyBlock(effective, schema, wide);
                        }

                        if (slotTouches)
                        {
                            ApplyBlock(effective, schema, slotBlock);
                        }

                        materializedAny = true;
                    }
                }

                result[index] = effective;
            }

            return materializedAny ? result : admittedMaterials;
        }

        private static int SingleOccupiedSlot(
            Material[] slotMaterials,
            Material material)
        {
            var found = -1;
            for (var slot = 0; slot < slotMaterials.Length; slot++)
            {
                if (ReferenceEquals(slotMaterials[slot], material))
                {
                    if (found >= 0)
                    {
                        return -1;
                    }

                    found = slot;
                }
            }

            return found;
        }
        /// <summary>
        /// Snapshots the renderer's effective block state: for every slot,
        /// the winning value of each schema entry that slot's material's
        /// shader declares, after per-index-over-renderer-wide precedence.
        /// Empty when no block is present. The snapshot holds no live block
        /// object, so it can be retained on prepared state and compared
        /// after the fact.
        /// </summary>
        internal static IReadOnlyList<BlockStateEntry> CaptureBlockState(
            Renderer renderer)
        {
            var entries = new List<BlockStateEntry>();
            if (!renderer.HasPropertyBlock())
            {
                return entries;
            }

            var slotMaterials = renderer.sharedMaterials;
            var schemas = new Dictionary<Shader, List<SchemaEntry>>();
            var wide = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(wide);
            for (var index = 0; index < slotMaterials.Length; index++)
            {
                var schema = SchemaFor(slotMaterials[index], schemas);
                if (schema == null)
                {
                    continue;
                }

                var perIndex = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(perIndex, index);
                foreach (var entry in schema)
                {
                    if (perIndex.HasProperty(entry.Name))
                    {
                        entries.Add(ReadEntry(index, entry, perIndex));
                    }
                    else if (wide.HasProperty(entry.Name))
                    {
                        entries.Add(ReadEntry(index, entry, wide));
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Exact comparison of two snapshots of the same renderer. The
        /// enumeration order is a pure function of slot order and shader
        /// property order, so equal states enumerate identically; a changed
        /// schema — a foreign material swap — also changes the enumeration
        /// and reads as a mismatch, which is the conservative direction.
        /// </summary>
        internal static bool BlockStateEquals(
            IReadOnlyList<BlockStateEntry> first,
            IReadOnlyList<BlockStateEntry> second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first == null || second == null ||
                first.Count != second.Count)
            {
                return false;
            }

            for (var index = 0; index < first.Count; index++)
            {
                if (!first[index].Equals(second[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static BlockStateEntry ReadEntry(
            int slotIndex,
            SchemaEntry schema,
            MaterialPropertyBlock block)
        {
            switch (schema.Type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                case ShaderPropertyType.Int:
                    return new BlockStateEntry(
                        slotIndex, schema.Name, schema.Type,
                        block.GetFloat(schema.Name), default, default, null);
                case ShaderPropertyType.Color:
                    return new BlockStateEntry(
                        slotIndex, schema.Name, schema.Type, 0f,
                        block.GetColor(schema.Name), default, null);
                case ShaderPropertyType.Vector:
                    return new BlockStateEntry(
                        slotIndex, schema.Name, schema.Type, 0f, default,
                        block.GetVector(schema.Name), null);
                case ShaderPropertyType.Texture:
                    return new BlockStateEntry(
                        slotIndex, schema.Name, schema.Type, 0f, default,
                        default, block.GetTexture(schema.Name));
                default:
                    return new BlockStateEntry(
                        slotIndex, schema.Name, schema.Type, 0f, default,
                        default, null);
            }
        }

        private readonly struct SchemaEntry
        {
            internal SchemaEntry(string name, ShaderPropertyType type)
            {
                Name = name;
                Type = type;
            }

            internal string Name { get; }
            internal ShaderPropertyType Type { get; }
        }

        private static List<SchemaEntry> SchemaFor(
            Material material,
            Dictionary<Shader, List<SchemaEntry>> schemas)
        {
            if (material == null || material.shader == null)
            {
                return null;
            }

            var shader = material.shader;
            if (schemas.TryGetValue(shader, out var cached))
            {
                return cached;
            }

            var entries = new List<SchemaEntry>();
            var count = shader.GetPropertyCount();
            for (var index = 0; index < count; index++)
            {
                var type = shader.GetPropertyType(index);
                var name = shader.GetPropertyName(index);
                entries.Add(new SchemaEntry(name, type));
                if (type == ShaderPropertyType.Texture)
                {
                    entries.Add(
                        new SchemaEntry(name + "_ST", ShaderPropertyType.Vector));
                    entries.Add(new SchemaEntry(
                        name + "_TexelSize", ShaderPropertyType.Vector));
                    entries.Add(new SchemaEntry(
                        name + "_HDR", ShaderPropertyType.Vector));
                }
            }

            schemas[shader] = entries;
            return entries;
        }

        private static bool TouchesSchema(
            List<SchemaEntry> schema,
            MaterialPropertyBlock block)
        {
            foreach (var entry in schema)
            {
                if (block.HasProperty(entry.Name))
                {
                    return true;
                }
            }

            return false;
        }
        private static bool TouchesTextureSchema(
            List<SchemaEntry> schema,
            MaterialPropertyBlock block)
        {
            foreach (var entry in schema)
            {
                if (entry.Type != ShaderPropertyType.Texture &&
                    !entry.Name.EndsWith("_ST", StringComparison.Ordinal) &&
                    !entry.Name.EndsWith("_TexelSize", StringComparison.Ordinal) &&
                    !entry.Name.EndsWith("_HDR", StringComparison.Ordinal))
                {
                    continue;
                }

                if (block.HasProperty(entry.Name))
                {
                    return true;
                }
            }

            return false;
        }


        private static void ApplyBlock(
            Material clone,
            List<SchemaEntry> schema,
            MaterialPropertyBlock block)
        {
            foreach (var entry in schema)
            {
                if (!block.HasProperty(entry.Name))
                {
                    continue;
                }

                switch (entry.Type)
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        clone.SetFloat(entry.Name, block.GetFloat(entry.Name));
                        break;
                    case ShaderPropertyType.Int:
                        clone.SetInteger(entry.Name, block.GetInt(entry.Name));
                        break;
                    case ShaderPropertyType.Color:
                        clone.SetColor(entry.Name, block.GetColor(entry.Name));
                        break;
                    case ShaderPropertyType.Vector:
                        clone.SetVector(entry.Name, block.GetVector(entry.Name));
                        break;
                    case ShaderPropertyType.Texture:
                        clone.SetTexture(entry.Name, block.GetTexture(entry.Name));
                        break;
                }
            }
        }
    }
}
