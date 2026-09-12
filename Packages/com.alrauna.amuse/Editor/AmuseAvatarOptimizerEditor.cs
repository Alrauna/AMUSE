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
                    "Smallest Tested Mipmap",
                    "A mipmap is a smaller copy of a texture. Each " +
                    "copy is half the size of the copy before it. " +
                    "The GPU shows the small copies when the avatar " +
                    "is far away or small on screen.\n\n" +
                    "A transparent texture often fades at small " +
                    "copies, because averaging mixes transparent " +
                    "texels into solid ones. A texel is one pixel of " +
                    "a texture. AMUSE checks the copies before it " +
                    "moves a triangle onto an opaque material. One " +
                    "faded texel in a checked copy stops the move. " +
                    "Textures with transparency can fail at a small " +
                    "copy for this reason.\n\n" +
                    "This setting sets the smallest copy AMUSE " +
                    "checks. AMUSE ignores every smaller copy. " +
                    "Moving toward mip 0 can improve performance at " +
                    "the cost of quality at a distance. Moving away " +
                    "from mip 0 can improve quality at a distance at " +
                    "the cost of performance. Mip 0 is the full " +
                    "texture. A 2048 by 2048 texture renders at 128 " +
                    "by 128 at mip 4. The default of Mip 4 fits most " +
                    "viewing distances. Select All Mips to check " +
                    "every copy. This is the safest choice.\n\n" +
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
                    "Smallest Tested Texture",
                    "This setting works like Smallest Tested " +
                    "Mipmap, but as an absolute size in texels " +
                    "instead of a mipmap number. A mipmap is a " +
                    "smaller copy of a texture. AMUSE stops checking " +
                    "a texture when a copy is smaller than this size " +
                    "on either side. A texture smaller than this " +
                    "size on either side is never checked, so its " +
                    "triangles never move.\n\n" +
                    "Select All Sizes to check every copy of every " +
                    "texture. Select a smaller size to check more " +
                    "copies when a part looks solid far away where " +
                    "it should show through. Select a larger size to " +
                    "check fewer copies when AMUSE moves too few " +
                    "triangles. The default of 128 fits most " +
                    "viewing distances."),
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
                    "Minimum Opaque Coverage (Per Material)",
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
                    "Alpha Upper Clamp (Per Texture)",
                    "Alpha is how strong transparency is. A value " +
                    "of 100 is fully opaque. Some mixed transparent " +
                    "and opaque materials hold texels that are " +
                    "nearly opaque but not truly opaque. Alpha at " +
                    "or above this value counts as opaque evidence. " +
                    "Lowering this value admits more nearly opaque " +
                    "texels. A texel is one pixel of a texture." +
                    "\n\n" +
                    "Admitted texels render fully opaque after a " +
                    "move, so alpha gradients can show edges between " +
                    "the split materials. The default of 100 is the " +
                    "safest choice. Lower the value when a material " +
                    "holds nearly opaque texels that keep its solid " +
                    "parts on the transparent material."),
                Mathf.Clamp(alphaProperty.intValue, 0, 100),
                0, 100);
            alphaProperty.intValue = minimumOpaqueAlpha;

            var coverageProperty = serializedObject.FindProperty(
                "_polygonMinimumOpaqueCoveragePercent");
            var polygonCoverage = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Minimum Opaque Coverage (Per Polygon)",
                    "The minimum percentage of a polygon's texels " +
                    "that must stay at or above the per-polygon " +
                    "clamp before AMUSE moves the polygon onto an " +
                    "opaque material. Texels below the clamp are " +
                    "strays. This keeps a few stray texels from " +
                    "keeping an intentional opaque face on the " +
                    "transparent material, which wastes performance " +
                    "on overdraw." +
                    "\n\n" +
                    "Lowering this value lets AMUSE ignore denser " +
                    "strays. Ignored strays render fully opaque " +
                    "after a move. The default of 100 is the safest " +
                    "choice: no stray share is ever ignored."),
                Mathf.Clamp(coverageProperty.intValue, 0, 100),
                0, 100);
            coverageProperty.intValue = polygonCoverage;

            var clampProperty = serializedObject.FindProperty(
                "_polygonAlphaUpperClampPercent");
            var polygonClamp = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Alpha Upper Clamp (Per Polygon)",
                    "Alpha below this value is a stray texel on the " +
                    "polygon. AMUSE ignores strays only while the " +
                    "coverage slider passes. Lowering this value " +
                    "admits higher alpha values as strays, including " +
                    "nearly opaque ones. A texel is one pixel of a " +
                    "texture." +
                    "\n\n" +
                    "Ignored strays render fully opaque after a " +
                    "move, so alpha gradients can lose their " +
                    "transparency. The default of 100 admits nothing " +
                    "and is the safest choice. Lower this value " +
                    "together with the coverage slider when faint " +
                    "strays keep an intentional opaque face on the " +
                    "transparent material."),
                Mathf.Clamp(clampProperty.intValue, 0, 100),
                0, 100);
            clampProperty.intValue =
                NormalizePolygonClamp(minimumOpaqueAlpha, polygonClamp);
        }

        /// <summary>
        /// Normalizes the drawn per-polygon clamp. A drawn clamp of 100
        /// is the inert sentinel: the build maps it to the inert noise
        /// bound, so the inspector must keep the stored 100 instead of
        /// pushing it to opaque minus one. A stored opaque clamp at or
        /// below zero reads as the inert 100, mirroring the build's
        /// mapper, so the drawn clamp keeps its value. Otherwise the
        /// inspector clamp keeps the tolerated band strictly below the
        /// opaque percent.
        /// </summary>
        internal static int NormalizePolygonClamp(
            int opaquePercent, int drawnClamp)
        {
            if (drawnClamp >= 100)
            {
                return 100;
            }

            var opaque = opaquePercent <= 0 ? 100 : opaquePercent;
            return AlphaPolicyBounds.ClampNoise(opaque, drawnClamp);
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