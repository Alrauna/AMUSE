using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal enum AnimatedPropertyKind
    {
        Scalar,
        ColorComponent,
        VectorComponent,
        TextureScaleOffsetComponent,
    }

    internal enum ProofRelevantBindingResolution
    {
        Irrelevant,
        RendererWide,
        UnrecognizedMaterialBinding,
    }

    internal readonly struct AnimatedPropertyRef
    {
        internal AnimatedPropertyRef(
            string propertyName,
            AnimatedPropertyKind kind,
            int componentIndex)
        {
            PropertyName = propertyName;
            Kind = kind;
            ComponentIndex = componentIndex;
        }

        internal string PropertyName { get; }
        internal AnimatedPropertyKind Kind { get; }
        internal int ComponentIndex { get; }
    }

    internal static class AnimationBindingResolution
    {
        private const string MaterialPrefix = "material.";
        internal const string TextureScaleOffsetSuffix = "_ST";

        internal static bool IsCompatibleRendererType(
            string bindingTypeName,
            string rendererTypeName)
        {
            if (string.IsNullOrEmpty(rendererTypeName) ||
                string.IsNullOrEmpty(bindingTypeName))
            {
                return true;
            }

            if (string.Equals(
                    bindingTypeName, rendererTypeName, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(
                    bindingTypeName, typeof(Renderer).FullName, StringComparison.Ordinal) ||
                string.Equals(
                    bindingTypeName, nameof(Renderer), StringComparison.Ordinal))
            {
                return true;
            }

            var bindingShort = ShortName(bindingTypeName);
            var rendererShort = ShortName(rendererTypeName);
            return string.Equals(
                       bindingShort, rendererShort, StringComparison.Ordinal) ||
                   string.Equals(
                       bindingShort, nameof(Renderer), StringComparison.Ordinal);
        }

        internal static bool TryParseMaterialSlotBindingFor(
            string bindingPath,
            string bindingTypeFullName,
            string propertyName,
            string rendererPath,
            string rendererTypeFullName,
            out int slotIndex)
        {
            return LiveAnimationObservation.TryParseMaterialSlotBinding(
                       propertyName, out slotIndex) &&
                   string.Equals(
                       bindingPath, rendererPath,
                       StringComparison.Ordinal) &&
                   IsCompatibleRendererType(
                       bindingTypeFullName, rendererTypeFullName);
        }

        private static string ShortName(string typeName)
        {
            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < typeName.Length
                ? typeName.Substring(lastDot + 1)
                : typeName;
        }

        internal static bool AddressesAnalyzedRenderer(
            string bindingPath,
            string bindingTypeName,
            string rendererPath,
            string rendererTypeName)
        {
            return string.Equals(
                       bindingPath, rendererPath, StringComparison.Ordinal) &&
                   IsCompatibleRendererType(bindingTypeName, rendererTypeName);
        }

        internal static IReadOnlyCollection<string>
            DeriveTextureScaleOffsetProperties(MaterialEvidenceRequest relevance)
        {
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));

            var properties = new List<string>();
            foreach (var texture in relevance.TextureProperties)
            {
                if ((texture.Evidence & TextureEvidenceKinds.ScaleOffset) != 0)
                {
                    properties.Add(
                        texture.PropertyName + TextureScaleOffsetSuffix);
                }
            }

            return properties.AsReadOnly();
        }

        internal static ProofRelevantBindingResolution ResolveProofRelevant(
            CapturedFloatBinding binding,
            string rendererPath,
            MaterialEvidenceRequest relevance,
            out AnimatedPropertyRef reference,
            string rendererTypeName = null)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));

            reference = default;
            if (!AddressesAnalyzedRenderer(
                    binding.Path, binding.TypeName, rendererPath, rendererTypeName))
            {
                return ProofRelevantBindingResolution.Irrelevant;
            }
            var scaleOffsetProperties =
                DeriveTextureScaleOffsetProperties(relevance);
            if (TryStripPrefix(
                    binding.PropertyName, MaterialPrefix, out var property))
            {
                if (TryResolveGeneratedProperty(
                        property,
                        relevance,
                        scaleOffsetProperties,
                        out reference))
                {
                    return ProofRelevantBindingResolution.RendererWide;
                }

                return CouldAddressRelevantProperty(
                        property, relevance, scaleOffsetProperties)
                    ? ProofRelevantBindingResolution.UnrecognizedMaterialBinding
                    : ProofRelevantBindingResolution.Irrelevant;
            }

            if (TryStripIndexedMaterialPrefix(
                    binding.PropertyName, out property) &&
                (TryResolveGeneratedProperty(
                     property,
                     relevance,
                     scaleOffsetProperties,
                     out reference) ||
                 CouldAddressRelevantProperty(
                     property, relevance, scaleOffsetProperties)))
            {
                reference = default;
                return ProofRelevantBindingResolution.UnrecognizedMaterialBinding;
            }

            return ProofRelevantBindingResolution.Irrelevant;
        }

        internal static bool IsUnrecognizedObjectMaterialBinding(
            CapturedObjectBinding binding,
            string rendererPath,
            MaterialEvidenceRequest relevance,
            string rendererTypeName = null)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));

            if (!AddressesAnalyzedRenderer(
                    binding.Path, binding.TypeName, rendererPath, rendererTypeName))
            {
                return false;
            }

            if (LiveAnimationObservation.TryParseMaterialSlotBinding(
                    binding.PropertyName, out _))
            {
                return false;
            }

            if (!TryStripPrefix(
                    binding.PropertyName, MaterialPrefix, out var property) &&
                !TryStripIndexedMaterialPrefix(
                    binding.PropertyName, out property))
            {
                return false;
            }

            return CouldAddressRelevantProperty(
                       property,
                       relevance,
                       DeriveTextureScaleOffsetProperties(relevance)) ||
                   CouldAddressAny(property, RequestedTextureNames(relevance));
        }

        private static IReadOnlyCollection<string> RequestedTextureNames(
            MaterialEvidenceRequest relevance)
        {
            var properties = new List<string>(relevance.TextureProperties.Count);
            foreach (var texture in relevance.TextureProperties)
            {
                properties.Add(texture.PropertyName);
            }

            return properties;
        }

        private static bool TryResolveGeneratedProperty(
            string property,
            MaterialEvidenceRequest relevance,
            IReadOnlyCollection<string> scaleOffsetProperties,
            out AnimatedPropertyRef reference)
        {
            reference = default;
            if (TrySplitComponent(property, "xyzw", out var stem, out var index) &&
                Enumerable.Contains(scaleOffsetProperties, stem, StringComparer.Ordinal))
            {
                reference = new AnimatedPropertyRef(
                    stem,
                    AnimatedPropertyKind.TextureScaleOffsetComponent,
                    index);
                return true;
            }

            if (TrySplitComponent(property, "rgba", out stem, out index) &&
                Enumerable.Contains(relevance.ColorProperties, stem, StringComparer.Ordinal))
            {
                reference = new AnimatedPropertyRef(
                    stem, AnimatedPropertyKind.ColorComponent, index);
                return true;
            }

            if (TrySplitComponent(property, "xyzw", out stem, out index) &&
                Enumerable.Contains(relevance.VectorProperties, stem, StringComparer.Ordinal))
            {
                reference = new AnimatedPropertyRef(
                    stem, AnimatedPropertyKind.VectorComponent, index);
                return true;
            }

            if (HasCharacterizedComponentSuffix(property) ||
                !Enumerable.Contains(relevance.ScalarProperties, property, StringComparer.Ordinal))
            {
                return false;
            }

            reference = new AnimatedPropertyRef(
                property, AnimatedPropertyKind.Scalar, -1);
            return true;
        }

        private static bool CouldAddressRelevantProperty(
            string property,
            MaterialEvidenceRequest relevance,
            IReadOnlyCollection<string> scaleOffsetProperties)
        {
            return CouldAddressAny(property, relevance.ScalarProperties) ||
                   CouldAddressAny(property, relevance.ColorProperties) ||
                   CouldAddressAny(property, relevance.VectorProperties) ||
                   CouldAddressAny(property, scaleOffsetProperties);
        }

        private static bool CouldAddressAny(
            string text,
            IEnumerable<string> relevantProperties)
        {
            if (text == null) return false;
            foreach (var property in relevantProperties)
            {
                if (string.Equals(text, property, StringComparison.Ordinal))
                    return true;
                if (text.Length > property.Length &&
                    text.StartsWith(property, StringComparison.Ordinal) &&
                    (text[property.Length] == '.' ||
                     text[property.Length] == '['))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySplitComponent(
            string property,
            string suffixes,
            out string stem,
            out int componentIndex)
        {
            stem = null;
            componentIndex = -1;
            if (property == null || property.Length < 3 ||
                property[property.Length - 2] != '.')
            {
                return false;
            }

            componentIndex = suffixes.IndexOf(property[property.Length - 1]);
            if (componentIndex < 0) return false;
            stem = property.Substring(0, property.Length - 2);
            return true;
        }

        private static bool HasCharacterizedComponentSuffix(string property)
        {
            return TrySplitComponent(property, "rgba", out _, out _) ||
                   TrySplitComponent(property, "xyzw", out _, out _);
        }

        private static bool TryStripPrefix(
            string text,
            string prefix,
            out string remainder)
        {
            remainder = null;
            if (text == null ||
                !text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            remainder = text.Substring(prefix.Length);
            return true;
        }

        private static bool TryStripIndexedMaterialPrefix(
            string text,
            out string property)
        {
            const string prefix = "material[";
            property = null;
            if (text == null ||
                !text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var closingBracket = text.IndexOf(']', prefix.Length);
            if (closingBracket < 0 || closingBracket + 1 >= text.Length ||
                text[closingBracket + 1] != '.')
            {
                return false;
            }

            property = text.Substring(closingBracket + 2);
            return true;
        }
    }
}
