using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Fixture policy helpers. Conversion fixtures use small textures
    /// that a real minimum size would leave unproven, so conversion
    /// tests pin the size policy to All Sizes unless the test is
    /// about the size policy itself.
    /// </summary>
    internal static class FixtureProofScope
    {
        /// <summary>The stored value meaning every size.</summary>
        internal const int AllSizes = -1;

        /// <summary>
        /// Pins the optimizer component on this root to All Sizes, so
        /// the fixture proves over the full mip chains.
        /// </summary>
        internal static void PinAllSizes(GameObject root)
        {
            var optimizer =
                root.GetComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            if (optimizer == null)
            {
                return;
            }

            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = AllSizes;
            serialized.FindProperty("_cutoutPreserveTransparencyMinTextureSize")
                .intValue = AllSizes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Stores desktop acceptance on this root's own optimizer through
        /// its serialized field, so the fixture's local footprint proof
        /// resolves under the accepted non-anisotropic policy. The
        /// precondition it states: the fixtures prove their opaque
        /// candidates against the narrow authored footprint, and only
        /// the accepted policy projects that footprint. The conservative
        /// desktop projection keeps the anisotropic footprint that
        /// refuses them. Each scenario callsite states its own
        /// acceptance. No global setup grants acceptance.
        /// </summary>
        internal static void AcceptDesktopAnisotropyDifferences(
            GameObject root)
        {
            var optimizer = root.GetComponent<
                Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_acceptDesktopAnisotropyDifferences")
                .boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
