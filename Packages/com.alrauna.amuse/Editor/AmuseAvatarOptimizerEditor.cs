using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Inspector for AmuseAvatarOptimizer. Shows placement guidance. The last
    /// build status arrives with the report channel slice.
    /// </summary>
    [CustomEditor(typeof(AmuseAvatarOptimizer))]
    public sealed class AmuseAvatarOptimizerEditor : UnityEditor.Editor
    {
        private bool _advancedOpen;
        private bool _alphaSeparatorOpen;

        public override void OnInspectorGUI()
        {
            DrawHeader();

            var component = (AmuseAvatarOptimizer)target;
            if (AmuseComponentPlacement.IsOnHierarchyRoot(component))
            {
                EditorGUILayout.HelpBox(
                    "AMUSE will run when you upload this avatar and when " +
                    "you enter Play mode. " +
                    "It moves proven opaque parts of transparent materials onto opaque copies. " +
                    "Anything it cannot prove stays unchanged and gets reported.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This component must sit on the root object of the avatar. " +
                    "Move it to the top object. " +
                    "The optimizer does not run while the component sits on a child.",
                    MessageType.Error);
            }

            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_amuseDisabled"),
                new GUIContent("Disable AMUSE",
                    "Treats this component as absent: nothing runs on build, " +
                    "in Play mode, or anywhere else, and nothing is reported."));
            serializedObject.ApplyModifiedProperties();

            DrawAlphaSeparator();
            DrawAdvancedSettings();

            if (AmuseBuildStatusStore.TryGet(
                    component.gameObject.GetInstanceID(), out var status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
        }


        /// <summary>
        /// The Alpha Separator foldout. It holds the user's policy for
        /// the alpha separation feature: which texture levels the
        /// opacity proof consults, and how big a split must be before
        /// AMUSE pays a draw call for it.
        /// </summary>
        private void DrawAlphaSeparator()
        {
            _alphaSeparatorOpen = EditorGUILayout.Foldout(
                _alphaSeparatorOpen, "Alpha Separator",
                EditorStyles.foldoutHeader);
            if (!_alphaSeparatorOpen)
            {
                return;
            }

            DrawMipCapPopup();
            DrawMinTextureSizePopup();
            DrawCoverageSlider();
            DrawAlphaPolicyControls();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMipCapPopup()
        {
            var property = serializedObject.FindProperty(
                "_preserveTransparencyMaxMipLevel");

            // Index 0 is "All Mips" (stored -1); index i maps to level i-1.
            var options = new[]
            {
                "All Mips", "Mip 0", "Mip 1", "Mip 2", "Mip 3", "Mip 4",
                "Mip 5", "Mip 6", "Mip 7", "Mip 8", "Mip 9", "Mip 10",
            };
            var stored = property.intValue;
            var index = stored < 0
                ? 0
                : Mathf.Min(stored + 1, options.Length - 1);
            var selected = EditorGUILayout.Popup(
                new GUIContent(
                    "Preserve Transparency Maximum Mipmap",
                    "A mipmap level is a smaller copy of the texture. " +
                    "Each level is half the size of the level before it. " +
                    "The GPU uses the small levels when the avatar is far " +
                    "away or small on screen.\n\n" +
                    "A transparent texture often fades at small levels. " +
                    "Averaging mixes transparent texels into texels that " +
                    "were solid. A texel is one pixel of a texture. " +
                    "AMUSE checks texture levels before it moves a " +
                    "triangle onto an opaque material. One faded texel in " +
                    "a checked level stops the move.\n\n" +
                    "This setting sets the largest level AMUSE checks. " +
                    "AMUSE ignores every level above it. Mip 4 fits most " +
                    "viewing distances and keeps most conversions. " +
                    "Select All Mips to check every level. This is the " +
                    "safest choice.\n\n" +
                    "Select a larger level when a part looks solid " +
                    "at long distance where it should show through. " +
                    "Select a smaller level when AMUSE moves too few " +
                    "triangles."),
                index,
                options);
            property.intValue = selected == 0 ? -1 : selected - 1;
        }

        private void DrawMinTextureSizePopup()
        {
            var sizeProperty = serializedObject.FindProperty(
                "_preserveTransparencyMinTextureSize");

            // Index 0 is "All Sizes" (stored -1); index i maps to 2^i.
            var sizeOptions = new string[14];
            sizeOptions[0] = "All Sizes";
            for (var option = 1; option < sizeOptions.Length; option++)
            {
                sizeOptions[option] = Mathf.RoundToInt(
                    Mathf.Pow(2, option)).ToString();
            }

            var storedSize = sizeProperty.intValue;
            var sizeIndex = 0;
            if (storedSize > 0)
            {
                sizeIndex = 1;
                while (sizeIndex < sizeOptions.Length - 1 &&
                       Mathf.RoundToInt(Mathf.Pow(2, sizeIndex)) <
                       storedSize)
                {
                    sizeIndex++;
                }
            }

            var selectedSize = EditorGUILayout.Popup(
                new GUIContent(
                    "Preserve Transparency Minimum Texture Size",
                    "A mipmap level is a smaller copy of the texture. " +
                    "This setting sets the smallest level size AMUSE " +
                    "checks. AMUSE stops checking a texture when a " +
                    "level is smaller than this size on either side. " +
                    "A texture smaller than this size on either side " +
                    "is never checked, so its triangles never move." +
                    "\n\n" +
                    "Select All Sizes to check every level of every " +
                    "texture. Select a smaller size to check more " +
                    "levels when a part looks solid far away where it " +
                    "should show through. Select a larger size to " +
                    "check fewer levels when AMUSE moves too few " +
                    "triangles."),
                sizeIndex,
                sizeOptions);
            sizeProperty.intValue = selectedSize == 0
                ? -1
                : Mathf.RoundToInt(Mathf.Pow(2, selectedSize));
        }

        private void DrawCoverageSlider()
        {
            var coverageProperty = serializedObject.FindProperty(
                "_minimumOpaqueCoveragePercent");
            coverageProperty.intValue = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Minimum Opaque Coverage Percentage",
                    "AMUSE moves proven opaque triangles of a mixed " +
                    "material onto a separate opaque material. Each " +
                    "split adds one draw call, and draw calls cost " +
                    "CPU time. This setting sets the smallest share " +
                    "of proven opaque triangles a mixed material " +
                    "needs before AMUSE does the split. The share " +
                    "counts every triangle of that material slot." +
                    "\n\n" +
                    "Use 0 to always split when at least one triangle " +
                    "is proven opaque. Raise the value to skip splits " +
                    "that move too little."),
                Mathf.Clamp(coverageProperty.intValue, 0, 100),
                0, 100);
        }

        private void DrawAlphaPolicyControls()
        {
            var alphaProperty = serializedObject.FindProperty(
                "_minimumOpaqueAlphaPercent");
            var minimumOpaqueAlpha = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Minimum Opaque Alpha Percentage",
                    "A texel is one pixel of a texture. AMUSE checks " +
                    "texel alpha before it moves a triangle onto an " +
                    "opaque material. This setting sets the smallest " +
                    "alpha percentage that counts as opaque evidence. " +
                    "It applies to materials without a shader cutoff." +
                    "\n\n" +
                    "Alpha between this value and full opacity becomes " +
                    "fully opaque after a move. The default of 100 is " +
                    "the safest choice. Raise the value when a part " +
                    "looks solid far away where it should show " +
                    "through."),
                Mathf.Clamp(alphaProperty.intValue, 0, 100),
                0, 100);
            alphaProperty.intValue = minimumOpaqueAlpha;

            var gateProperty = serializedObject.FindProperty(
                "_transparencyNoiseGatePercent");
            var gate = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Transparency Noise Gate Percentage",
                    "A transparent texture often holds faint stray " +
                    "texels that never show. Alpha below this value " +
                    "is noise. AMUSE ignores noise when the noise is " +
                    "sparse. This applies to materials without a " +
                    "shader cutoff." +
                    "\n\n" +
                    "Noise that AMUSE ignores becomes fully opaque " +
                    "after a move. A texture without a source file " +
                    "gives AMUSE only its published mip levels, so " +
                    "the gate is weaker on it. Raise the value when " +
                    "faint strays stop moves that should happen."),
                Mathf.Clamp(gateProperty.intValue, 0, 100),
                0, 100);
            gateProperty.intValue =
                AlphaPolicyBounds.ClampNoise(minimumOpaqueAlpha, gate);

            var texelProperty = serializedObject.FindProperty(
                "_maximumNoiseTexelPercent");
            texelProperty.intValue = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Maximum Noise Texel Percentage",
                    "This setting works with the noise gate. The gate " +
                    "fires for one polygon only when its noise texels " +
                    "are strictly under this share of the texels AMUSE " +
                    "checks for that polygon." +
                    "\n\n" +
                    "Noise that AMUSE ignores becomes fully opaque " +
                    "after a move. Raise the value to ignore denser " +
                    "noise. Raise it only when a part looks solid far " +
                    "away where it should show through."),
                Mathf.Clamp(texelProperty.intValue, 0, 100),
                0, 100);
        }

        /// <summary>
        /// The Advanced Settings foldout. It holds the animation-closure
        /// tolerance setting: the rule for animations that name material
        /// slots this mesh does not have. Most users never touch it.
        /// </summary>
        private void DrawAdvancedSettings()
        {
            _advancedOpen = EditorGUILayout.Foldout(
                _advancedOpen, "Advanced Settings", EditorStyles.foldoutHeader);
            if (!_advancedOpen)
            {
                return;
            }

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots"),
                new GUIContent(
                    "Ignore Out-of-Range Material Slots",
                    "Some animation files animate material slots that do not exist on this mesh. " +
                    "By default, AMUSE refuses to optimize this mesh to prevent visual errors. " +
                    "Turn this setting on to ignore these extra slot animations and optimize the valid slots.\n\n" +
                    "Risk: If an animation later changes this mesh to have more material slots, " +
                    "or if another tool relies on the untouched slot count, visual errors can occur."));
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Centered product title with the package version to its right, in
        /// the style of the optimizers users already know, plus a one-click
        /// link to the issue tracker. The title is centered across the whole
        /// row, so its position never depends on the version width. The
        /// version is read from the installed package metadata, so a release
        /// never needs an inspector edit.
        /// </summary>
        private static void DrawHeader()
        {
            var row = EditorGUILayout.GetControlRect(GUILayout.Height(26));
            var centered = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
            };
            GUI.Label(row, "AMUSE", centered);

            var version = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AmuseAvatarOptimizer).Assembly)?.version;
            if (!string.IsNullOrEmpty(version))
            {
                var right = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                };
                GUI.Label(row, "v" + version, right);
            }

            if (GUILayout.Button("Report a bug"))
            {
                Application.OpenURL("https://github.com/Alrauna/AMUSE/issues");
            }

            EditorGUILayout.Space(4);
        }
    }
}