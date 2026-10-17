using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Editor.Presets;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Inspector for AmuseAvatarOptimizer. Shows placement guidance. The last
    /// build status arrives with the report channel slice.
    /// </summary>
    [CustomEditor(typeof(AmuseAvatarOptimizer))]
    public sealed class AmuseAvatarOptimizerEditor : UnityEditor.Editor
    {
        private bool _settingsOpen;
        private bool _alphaSeparatorOpen = true;

        /// <summary>
        /// The two submenu states live in the editor session, like
        /// the foldout states above. Both submenus start open. No
        /// serialized field stores them, so presets, Undo, and
        /// Reset stay untouched.
        /// </summary>
        private bool _transparentSettingsOpen = true;
        private bool _cutoutSettingsOpen = true;

        /// <summary>
        /// The two foldout states live in the editor session, like
        /// the two states above. No serialized field stores them, so
        /// presets, Undo, and Reset stay untouched.
        /// </summary>
        private bool _materialTransformationsOpen;
        private bool _alphaSeparationTransformationsOpen = true;

        /// <summary>
        /// The current dry run instance. It owns its own renderer
        /// index, so replacing it restarts the pass sequence, and
        /// keeping it across passes lets one renderer compute per
        /// repaint until the run completes.
        /// </summary>
        private AlphaSeparationDryRun _dryRun;

        /// <summary>
        /// True when the dry run completed on or before the layout pass.
        /// It latches during Layout events to prevent layout mismatches.
        /// </summary>
        private bool _dryRunCompletedLatched;

        internal static Action<MaterialTransformationRow, Transform> OnTransformationRowDrawnForTest;
        internal static Action<string, MessageType> OnHelpBoxDrawnForTest;
        internal static Action<string, GUIStyle> OnRendererLabelDrawnForTest;
        internal static Action<UnityEngine.Object, Type, bool> OnRendererObjectFieldDrawnForTest;
        internal static Action<UnityEngine.Object, Type, bool> OnMaterialObjectFieldDrawnForTest;
        internal static Action<string> OnButtonDrawnForTest;
        internal static Func<string, bool> ButtonClickOverrideForTest;
        internal static Action<string> OnFoldoutHeaderDrawnForTest;

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            _dryRun?.Cancel(timeoutMs: 200);
            _dryRun = null;
        }

        internal void OnBeforeAssemblyReload()
        {
            _dryRun?.Cancel(timeoutMs: 200);
            _dryRun = null;
        }

        internal void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode)
            {
                _dryRun?.Cancel(timeoutMs: 100);
                _dryRun = null;
            }
        }

        /// <summary>
        /// Steps the dry run during editor updates.
        /// </summary>
        internal void OnEditorUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (!_materialTransformationsOpen || !_alphaSeparationTransformationsOpen)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (_dryRun != null && !_dryRun.Done)
            {
                _dryRun.Step(synchronous: false);
                if (_dryRun.Done)
                {
                    RequestHostRepaint();
                }
            }
            else if (_dryRun != null && _dryRun.Done && !_dryRunCompletedLatched)
            {
                RequestHostRepaint();
            }
        }

        /// <summary>
        /// The host repaint override for the verified harness. The
        /// production host is the inspector window, so null keeps the
        /// editor's own Repaint, and the harness aims the repaint at
        /// its utility window, which an inspector repaint never
        /// reaches.
        /// </summary>
        internal Action HostRepaintOverride { get; set; }

        private List<OptimizerPreset> _presets;
        private string _presetProblemFile;
        private PresetLoadRefusal _presetProblem;

        public override void OnInspectorGUI()
        {
            DrawHeader();
            DrawPresetRow();

            var component = (AmuseAvatarOptimizer)target;
            if (component.transform.parent == null)
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
                serializedObject.FindProperty("_alphaSeparatorEnabled"),
                new GUIContent("Alpha Separator",
                    "Turn this off to skip alpha separation on this " +
                    "avatar. Nothing is analyzed, moved, or reported " +
                    "for the feature. A later feature adds its own " +
                    "switch beside this one."));
            serializedObject.ApplyModifiedProperties();

            DrawMaterialTransformations();

            if (DrawSettings())
            {
                DrawAlphaSeparatorSettings();
            }

            if (AmuseBuildStatusStore.TryGet(out var status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
        }

        /// <summary>The dry run driver, exposed internally for verified tests.</summary>
        internal AlphaSeparationDryRun DryRun => _dryRun;

        /// <summary>
        /// The Show Material Transformations foldout with the nested
        /// Alpha Separator view. The view shows outcomes, not
        /// policy, so it sits outside the Advanced Settings reveal,
        /// like the Alpha Separator toggle, and no serialized field
        /// stores its state.
        /// </summary>
        private void DrawMaterialTransformations()
        {
            _materialTransformationsOpen = EditorGUILayout.Foldout(
                _materialTransformationsOpen,
                "Show Material Transformations",
                EditorStyles.foldoutHeader);
            OnFoldoutHeaderDrawnForTest?.Invoke("Show Material Transformations");
            if (!_materialTransformationsOpen)
            {
                return;
            }

            var optimizer = (AmuseAvatarOptimizer)target;
            var avatarRoot = optimizer?.transform;
            var avatarKey = avatarRoot != null
                ? PlayModeBuildStore.ComputeAvatarKey(avatarRoot)
                : null;

            if (GUILayout.Button("Refresh"))
            {
                if (!string.IsNullOrEmpty(avatarKey))
                {
                    PlayModeBuildStore.Clear(avatarKey);
                }

                _dryRun?.Cancel();
                DryRunCache.Clear();
                _dryRun = null;
                _dryRunCompletedLatched = false;
            }

            _alphaSeparationTransformationsOpen = EditorGUILayout.Foldout(
                _alphaSeparationTransformationsOpen,
                "Alpha Separator",
                EditorStyles.foldoutHeader);
            OnFoldoutHeaderDrawnForTest?.Invoke("Alpha Separator");
            if (!_alphaSeparationTransformationsOpen)
            {
                return;
            }

            EditorGUILayout.LabelField(
                RowText.DisclosureSentence(0),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                RowText.DisclosureSentence(1),
                EditorStyles.wordWrappedLabel);

            if (Event.current.type == EventType.Layout)
            {
                _dryRunCompletedLatched = _dryRun != null && _dryRun.Done;
            }

            var fingerprint = optimizer != null
                ? PlayModeBuildStore.ComputePolicyFingerprint(
                    optimizer, EditorUserBuildSettings.activeBuildTarget)
                : null;
            var playRecord = (!string.IsNullOrEmpty(avatarKey) && !string.IsNullOrEmpty(fingerprint))
                ? PlayModeBuildStore.TryLoad(avatarKey, fingerprint)
                : null;

            if (playRecord != null)
            {
                const string playSentence =
                    "This view shows accurate results from the Play mode build.";
                EditorGUILayout.HelpBox(playSentence, MessageType.Info);
                OnHelpBoxDrawnForTest?.Invoke(playSentence, MessageType.Info);

                if (playRecord.avatarRows != null)
                {
                    foreach (var serializedRow in playRecord.avatarRows)
                    {
                        DrawTransformationRow(
                            MaterialTransformationRow.FromSerialized(serializedRow),
                            avatarRoot);
                    }
                }

                if (playRecord.rendererRows != null)
                {
                    foreach (var serializedRow in playRecord.rendererRows)
                    {
                        DrawTransformationRow(
                            MaterialTransformationRow.FromSerialized(serializedRow),
                            avatarRoot);
                    }
                }

                return;
            }

            if (_dryRun == null)
            {
                _dryRun = new AlphaSeparationDryRun(
                    optimizer,
                    () => EditorUserBuildSettings.activeBuildTarget);
                _dryRun.RepaintRequested = RequestHostRepaint;
                _dryRun.Begin();
                _dryRunCompletedLatched = false;
                RequestHostRepaint();
            }

            if (_dryRun != null && _dryRun.IsCancelled)
            {
                EditorGUILayout.HelpBox("AMUSE material transformation analysis was cancelled.", MessageType.Info);
                OnHelpBoxDrawnForTest?.Invoke("AMUSE material transformation analysis was cancelled.", MessageType.Info);
                OnButtonDrawnForTest?.Invoke("Compute");
                if (GUILayout.Button("Compute", GUILayout.Width(80)) ||
                    (ButtonClickOverrideForTest != null && ButtonClickOverrideForTest("Compute")))
                {
                    _dryRun = null;
                    _dryRunCompletedLatched = false;
                }
                return;
            }

            if (!_dryRunCompletedLatched)
            {
                var text = _dryRun != null && _dryRun.TotalRendererCount > 0
                    ? $"AMUSE is computing material transformations: renderer {_dryRun.CurrentRendererIndex + 1} of {_dryRun.TotalRendererCount}."
                    : "AMUSE is computing material transformations.";
                EditorGUILayout.HelpBox(text, MessageType.Info);
                OnHelpBoxDrawnForTest?.Invoke(text, MessageType.Info);
                OnButtonDrawnForTest?.Invoke("Cancel");
                if (GUILayout.Button("Cancel", GUILayout.Width(60)) ||
                    (ButtonClickOverrideForTest != null && ButtonClickOverrideForTest("Cancel")))
                {
                    _dryRun?.Cancel();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            foreach (var row in _dryRun.AvatarRows)
            {
                DrawTransformationRow(row, avatarRoot);
            }

            foreach (var row in _dryRun.RendererRows)
            {
                DrawTransformationRow(row, avatarRoot);
            }
        }

        /// <summary>
        /// One display row. A banner row, an errored row, and a
        /// refused renderer row draw as warnings. A slot row renders
        /// the renderer object field and material object field side by
        /// side on line one. When the renderer is null, a bold label
        /// renders the path instead. The second line shows the indented
        /// status sentence. Every other row draws as a plain line.
        /// </summary>
        private static void DrawTransformationRow(
            MaterialTransformationRow row,
            Transform avatarRoot)
        {
            OnTransformationRowDrawnForTest?.Invoke(row, avatarRoot);
            switch (row.Kind)
            {
                case MaterialTransformationRowKind.Banner:
                case MaterialTransformationRowKind.Errored:
                    EditorGUILayout.HelpBox(row.Sentence, MessageType.Warning);
                    return;
                case MaterialTransformationRowKind.Renderer:
                    EditorGUILayout.HelpBox(row.Sentence, MessageType.Warning);
                    return;
                case MaterialTransformationRowKind.Slot:
                    Renderer renderer = null;
                    if (avatarRoot != null)
                    {
                        renderer = string.IsNullOrEmpty(row.RendererPath)
                            ? avatarRoot.GetComponent<Renderer>()
                            : avatarRoot.Find(row.RendererPath)?.GetComponent<Renderer>();
                    }

                    Material material = null;
                    var materials = renderer != null ? renderer.sharedMaterials : null;
                    if (materials != null && row.SlotIndex >= 0 && row.SlotIndex < materials.Length)
                    {
                        material = materials[row.SlotIndex];
                    }

                    if (!string.IsNullOrEmpty(row.SourceMaterialPath))
                    {
                        var currentPath = material != null ? AssetDatabase.GetAssetPath(material) : null;
                        if (currentPath != row.SourceMaterialPath)
                        {
                            var loaded = AssetDatabase.LoadAssetAtPath<Material>(row.SourceMaterialPath);
                            if (loaded != null)
                            {
                                material = loaded;
                            }
                        }
                    }

                    int indentOffset = EditorGUI.indentLevel * 15;
                    float halfWidth = Mathf.Max(60f, (EditorGUIUtility.currentViewWidth - indentOffset) / 2f - 20f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (renderer != null)
                        {
                            EditorGUILayout.ObjectField(renderer, typeof(Renderer), true, GUILayout.Width(halfWidth));
                            OnRendererObjectFieldDrawnForTest?.Invoke(renderer, typeof(Renderer), true);
                        }
                        else
                        {
                            var pathLabel = row.RendererPath ?? string.Empty;
                            EditorGUILayout.LabelField(pathLabel, EditorStyles.boldLabel, GUILayout.Width(halfWidth));
                            OnRendererLabelDrawnForTest?.Invoke(pathLabel, EditorStyles.boldLabel);
                        }

                        int originalIndent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;
                        EditorGUILayout.ObjectField(material, typeof(Material), false);
                        OnMaterialObjectFieldDrawnForTest?.Invoke(material, typeof(Material), false);
                        EditorGUI.indentLevel = originalIndent;
                    }

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(row.Sentence);
                    EditorGUI.indentLevel--;
                    return;
                default:
                    EditorGUILayout.LabelField(row.Sentence);
                    return;
            }
        }

        /// <summary>
        /// The repaint the dry run asks for after an advancing pass.
        /// The production host is the inspector window, and the
        /// editor's own Repaint targets that window. The verified
        /// harness aims the repaint at its utility window instead,
        /// because an inspector repaint never reaches it.
        /// </summary>
        private void RequestHostRepaint()
        {
            (HostRepaintOverride ?? Repaint)();
        }


        /// <summary>
        /// The preset row. One button per shipped preset file, in the
        /// fixed order safe, normal, aggressive. A button presses only
        /// while the component matches that preset on all sixteen
        /// fields, so the pressed state is always honest and never
        /// stored. A broken preset file turns the row off with a
        /// warning, because a row over a partial preset universe could
        /// claim a match it cannot prove.
        /// </summary>
        private void DrawPresetRow()
        {
            if (_presets == null)
            {
                PresetFileStore.TryLoadAll(
                    out _presets, out _presetProblemFile,
                    out _presetProblem);
            }
            if (_presetProblem != PresetLoadRefusal.None)
            {
                EditorGUILayout.HelpBox(
                    "The preset file " + _presetProblemFile + " is not " +
                    "valid: " + _presetProblem + ". The preset row " +
                    "stays off. Fix the file or reinstall the package.",
                    MessageType.Warning);
                return;
            }

            var component = (AmuseAvatarOptimizer)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "Presets", EditorStyles.boldLabel, GUILayout.Width(50));
                foreach (var preset in _presets)
                {
                    var matched = preset.Matches(component);
                    var clicked = GUILayout.Toggle(
                        matched,
                        new GUIContent(preset.Name, preset.Description),
                        GUI.skin.button);
                    if (clicked && !matched)
                    {
                        serializedObject.Update();
                        PresetApplier.Apply(preset, serializedObject);
                    }
                }
            }
        }

        private static readonly GUIContent MipCapRowContent = new GUIContent(
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
            "triangles.");

        private static readonly GUIContent MinTextureRowContent = new GUIContent(
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
            "viewing distances.");

        private static readonly GUIContent MaterialCoverageRowContent =
            new GUIContent(
                "Minimum Opaque Coverage to Convert (Per Material)",
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
                "that move too little.");

        private static readonly GUIContent PreserveCoverageRowContent =
            new GUIContent(
                "Minimum Opaque Coverage to Preserve (Per Material)",
                "AMUSE splits a mixed material. The proven opaque " +
                "triangles move onto a separate opaque material. " +
                "Each split adds one draw call. Some materials " +
                "are almost fully opaque. A split for such a " +
                "material can cost more than it saves. A material " +
                "whose unproven share sits below this value skips " +
                "the split. It moves onto the opaque material in " +
                "full. No proof is needed for the move. Triangles " +
                "that would keep their transparency lose it. The " +
                "share counts the triangles of one submesh. A " +
                "material slot that binds several submeshes " +
                "flattens only when every one of its submeshes " +
                "sits below this value. This setting works " +
                "together with Minimum Opaque Coverage to Convert " +
                "(Per Material). The convert setting decides " +
                "whether a split is big enough to pay for. This " +
                "setting decides whether a material is opaque " +
                "enough to skip the split. The value 0 never " +
                "flattens a material. It is the default.");

        private static readonly GUIContent TextureClampRowContent =
            new GUIContent(
                "Alpha Upper Clamp to Convert (Per Texture)",
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
                "parts on the transparent material.");

        private static readonly GUIContent PolygonCoverageRowContent =
            new GUIContent(
                "Minimum Opaque Coverage to Convert (Per Polygon)",
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
                "choice: no stray share is ever ignored.");

        private static readonly GUIContent PolygonClampRowContent =
            new GUIContent(
                "Alpha Upper Clamp to Convert (Per Polygon)",
                "Alpha below this value is a stray texel on the " +
                "polygon. AMUSE ignores strays only while the " +
                "coverage slider passes. Lowering this value " +
                "admits higher alpha values as strays, including " +
                "nearly opaque ones. A texel is one pixel of a " +
                "texture." +
                "\n\n" +
                "Ignored strays render fully opaque after a move, " +
                "so alpha gradients can lose their " +
                "transparency. The default of 100 admits nothing " +
                "and is the safest choice. Lower this value " +
                "together with the coverage slider when faint " +
                "strays keep an intentional opaque face on the " +
                "transparent material.");

        private static readonly GUIContent AcceptDesktopRowContent =
            new GUIContent(
                "Accept Desktop Anisotropy Differences",
                "Lets AMUSE prove opacity without anisotropic " +
                "footprints when the build target is Windows. " +
                "VRChat can filter textures anisotropically. " +
                "Proven parts can lose transparency near texture " +
                "edges or at oblique views. This setting does not " +
                "change texture filtering. The Safe, Normal, " +
                "and Aggressive presets turn this setting on.");

        private static readonly GUIContent AcceptMobileRowContent =
            new GUIContent(
                "Accept Mobile Anisotropy Differences",
                "Lets AMUSE prove opacity without anisotropic " +
                "footprints when the build target is Android or " +
                "iOS. AMUSE texture capture does not support " +
                "these targets. This setting alone enables no " +
                "conversion. The Safe, Normal, and Aggressive " +
                "presets turn this setting on.");

        private static readonly GUIContent ActiveSamplingProofRowContent =
            new GUIContent(
                "Active Sampling Proof",
                "The proof mode the current build target reads. " +
                "Changing the build target changes which " +
                "acceptance setting the build reads. It never " +
                "writes a value.");

        private static readonly GUIContent CutoutMipCapRowContent =
            new GUIContent(
                "Smallest Tested Mipmap",
                "A mipmap is a smaller copy of a texture. Each " +
                "copy is half the size of the copy before it. " +
                "The GPU shows the small copies when the avatar " +
                "is far away or small on screen. A cutout " +
                "texture uses a hard cutoff. A texel at or above " +
                "the cutoff is opaque. A texel below it is " +
                "discarded. The cutout proof checks the copies " +
                "before it moves a triangle onto an opaque " +
                "material. One unproven texel in a checked copy " +
                "stops the move. This setting sets the smallest " +
                "copy the cutout proof checks. AMUSE ignores " +
                "every smaller copy. Moving toward mip 0 can " +
                "improve performance at the cost of quality at a " +
                "distance. Moving away from mip 0 can improve " +
                "quality at a distance at the cost of " +
                "performance. Mip 0 is the full texture. A 2048 " +
                "by 2048 texture renders at 128 by 128 at mip 4. " +
                "The default of Mip 4 fits most viewing " +
                "distances. Select All Mips to check every copy. " +
                "This is the safest choice. Select a larger " +
                "level when a cutout part looks solid at long " +
                "distance where it should show through. Select a " +
                "smaller level when AMUSE moves too few cutout " +
                "triangles. This setting governs cutout sources " +
                "only. The Transparent submenu holds the " +
                "transparent setting.");

        private static readonly GUIContent CutoutMinTextureRowContent =
            new GUIContent(
                "Smallest Tested Texture",
                "This setting works like the cutout Smallest " +
                "Tested Mipmap, but as an absolute size in " +
                "texels instead of a mipmap number. A mipmap is " +
                "a smaller copy of a texture. The cutout proof " +
                "stops checking a texture when a copy is smaller " +
                "than this size on either side. A texture smaller " +
                "than this size on either side is never checked, " +
                "so its triangles never move. Select All Sizes to " +
                "check every copy of every texture. Select a " +
                "smaller size to check more copies when a cutout " +
                "part looks solid far away where it should show " +
                "through. Select a larger size to check fewer " +
                "copies when AMUSE moves too few cutout " +
                "triangles. The default of 128 fits most viewing " +
                "distances. This setting governs cutout sources " +
                "only. The Transparent submenu holds the " +
                "transparent setting.");

        private static readonly GUIContent CutoutMaterialCoverageRowContent =
            new GUIContent(
                "Minimum Opaque Coverage to Convert (Per Material)",
                "AMUSE moves proven opaque triangles of a mixed " +
                "cutout material onto a separate opaque material. " +
                "Each split adds one draw call, and draw calls " +
                "cost CPU time. This setting sets the smallest " +
                "share of proven opaque triangles a mixed cutout " +
                "material needs before AMUSE does the split. The " +
                "share counts every triangle of that material " +
                "slot. Use 0 to always split when at least one " +
                "triangle is proven opaque. Raise the value to " +
                "skip splits that move too little. This setting " +
                "governs cutout sources only. The Transparent " +
                "submenu holds the transparent setting.");

        private static readonly GUIContent CutoutPreserveCoverageRowContent =
            new GUIContent(
                "Minimum Opaque Coverage to Preserve (Per Material)",
                "AMUSE splits a mixed cutout material. The proven " +
                "opaque triangles move onto a separate opaque " +
                "material. Each split adds one draw call. Some " +
                "cutout materials are almost fully opaque. A " +
                "split for such a material can cost more than it " +
                "saves. A material whose unproven share sits " +
                "below this value skips the split. It moves onto " +
                "the opaque material in full. No proof is needed " +
                "for the move. Triangles that would keep their " +
                "transparency lose it. The share counts the " +
                "triangles of one submesh. A material slot that " +
                "binds several submeshes flattens only when every " +
                "one of its submeshes sits below this value. This " +
                "setting works together with the cutout Minimum " +
                "Opaque Coverage to Convert (Per Material). The " +
                "convert setting decides whether a split is big " +
                "enough to pay for. This setting decides whether " +
                "a material is opaque enough to skip the split. " +
                "The value 0 never flattens a material. It is the " +
                "Safe preset value. This setting governs cutout " +
                "sources only. The Transparent submenu holds the " +
                "transparent setting.");

        /// <summary>
        /// The Alpha Separator Settings row contents in draw order.
        /// One source of truth feeds both the draw calls and the
        /// shared label width, so a label change cannot drift away
        /// from its measurement. The Transparent submenu draws the
        /// first seven rows. The acceptance rows sit between the
        /// submenus and stay outside them. The Cutout submenu draws
        /// the last four rows.
        /// </summary>
        private static readonly GUIContent[] RowContents =
        {
            MipCapRowContent,
            MinTextureRowContent,
            MaterialCoverageRowContent,
            PreserveCoverageRowContent,
            TextureClampRowContent,
            PolygonCoverageRowContent,
            PolygonClampRowContent,
            AcceptDesktopRowContent,
            AcceptMobileRowContent,
            ActiveSamplingProofRowContent,
            CutoutMipCapRowContent,
            CutoutMinTextureRowContent,
            CutoutMaterialCoverageRowContent,
            CutoutPreserveCoverageRowContent,
        };

        /// <summary>
        /// The Alpha Separator Settings foldout. It holds the user's
        /// policy for the alpha separation feature: which texture
        /// levels the opacity proof consults, and how big a split must
        /// be before AMUSE pays a draw call for it. It stays hidden
        /// until the Advanced Settings reveal in Settings is on,
        /// because most users never change this policy. The body
        /// draws the acceptance group first. The two submenus follow.
        /// </summary>
        private void DrawAlphaSeparatorSettings()
        {
            _alphaSeparatorOpen = EditorGUILayout.Foldout(
                _alphaSeparatorOpen, "Alpha Separator Settings",
                EditorStyles.foldoutHeader);
            OnFoldoutHeaderDrawnForTest?.Invoke("Alpha Separator Settings");
            if (!_alphaSeparatorOpen)
            {
                return;
            }

            var widest = RowContents.Max(
                content => EditorStyles.label.CalcSize(content).x);
            var previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Ceil(widest) + 2f;
            DrawAcceptanceControls();
            DrawTransparentSubmenu();
            DrawCutoutSubmenu();
            EditorGUIUtility.labelWidth = previousWidth;
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// The Transparent submenu. It holds the seven policy rows
        /// that moved under it. The rows draw through the existing
        /// methods with their existing arguments. A closed submenu
        /// draws nothing.
        /// </summary>
        private void DrawTransparentSubmenu()
        {
            _transparentSettingsOpen = EditorGUILayout.Foldout(
                _transparentSettingsOpen, "Transparent",
                EditorStyles.foldoutHeader);
            OnFoldoutHeaderDrawnForTest?.Invoke("Transparent");
            if (!_transparentSettingsOpen)
            {
                return;
            }

            DrawMipCapPopup(
                "_preserveTransparencyMaxMipLevel", MipCapRowContent);
            DrawMinTextureSizePopup(
                "_preserveTransparencyMinTextureSize",
                MinTextureRowContent);
            DrawCoverageSlider();
            PercentSlider("_minimumOpaqueCoverageToPreservePercent",
                PreserveCoverageRowContent);
            DrawAlphaPolicyControls();
        }

        /// <summary>
        /// The Cutout submenu. It holds the four cutout rows. The
        /// popups share the transparent draw helpers with the cutout
        /// property paths and contents. The coverage rows call
        /// PercentSlider directly.
        /// </summary>
        private void DrawCutoutSubmenu()
        {
            _cutoutSettingsOpen = EditorGUILayout.Foldout(
                _cutoutSettingsOpen, "Cutout",
                EditorStyles.foldoutHeader);
            OnFoldoutHeaderDrawnForTest?.Invoke("Cutout");
            if (!_cutoutSettingsOpen)
            {
                return;
            }

            DrawMipCapPopup(
                "_cutoutPreserveTransparencyMaxMipLevel",
                CutoutMipCapRowContent);
            DrawMinTextureSizePopup(
                "_cutoutPreserveTransparencyMinTextureSize",
                CutoutMinTextureRowContent);
            PercentSlider("_cutoutMinimumOpaqueCoveragePercent",
                CutoutMaterialCoverageRowContent);
            PercentSlider("_cutoutMinimumOpaqueCoverageToPreservePercent",
                CutoutPreserveCoverageRowContent);
        }

        /// <summary>
        /// The two persistent acceptance toggles and the proof mode
        /// the active build target reads. The toggles store through
        /// the serialized object, so Undo and Reset treat them like
        /// every other control. The proof row calls TrySelect on each
        /// repaint and persists nothing, so a target switch only
        /// changes which setting the build reads and never writes a
        /// value.
        /// </summary>
        private void DrawAcceptanceControls()
        {
            var desktop = serializedObject.FindProperty(
                "_acceptDesktopAnisotropyDifferences");
            var mobile = serializedObject.FindProperty(
                "_acceptMobileAnisotropyDifferences");
            EditorGUILayout.PropertyField(desktop, AcceptDesktopRowContent);
            EditorGUILayout.PropertyField(mobile, AcceptMobileRowContent);

            var target = EditorUserBuildSettings.activeBuildTarget;
            var selected = PlatformAlphaSamplingPolicy.TrySelect(
                target,
                desktop.boolValue,
                mobile.boolValue,
                out var policy);
            string proof;
            if (!selected)
            {
                proof = "Unsupported build target";
            }
            else
            {
                // A successful selection leaves exactly two groups:
                // the Windows target is Desktop, Android and iOS are
                // Mobile.
                var group = target == BuildTarget.StandaloneWindows64
                    ? "Desktop"
                    : "Mobile";
                proof = group + ": " + (policy ==
                    AlphaSamplingPolicy.AcceptedNonAnisotropic
                    ? "Accepted non-anisotropic proof"
                    : "Conservative anisotropic proof");
            }
            EditorGUILayout.LabelField(
                ActiveSamplingProofRowContent,
                new GUIContent(proof));

            if (!selected)
            {
                EditorGUILayout.HelpBox(
                    "AMUSE does not support sampling-policy " +
                    "selection for this build target.",
                    MessageType.Warning);
                return;
            }
            if (policy == AlphaSamplingPolicy.AcceptedNonAnisotropic)
            {
                EditorGUILayout.HelpBox(
                    "AMUSE proves opacity without anisotropic " +
                    "footprints. VRChat can use anisotropic " +
                    "filtering. Converted parts can lose " +
                    "transparency near texture edges or at oblique " +
                    "views. This does not change texture filtering.",
                    MessageType.Warning);
            }
            if (target == BuildTarget.Android || target == BuildTarget.iOS)
            {
                EditorGUILayout.HelpBox(
                    "This target selects the mobile acceptance " +
                    "setting. AMUSE texture capture does not " +
                    "support this target. This setting does not " +
                    "enable mobile conversion.",
                    MessageType.Warning);
            }
        }

        private void DrawMipCapPopup(string propertyPath, GUIContent content)
        {
            var property = serializedObject.FindProperty(propertyPath);

            // Index 0 is "All Mips" (stored -1); index i maps to level i-1.
            var options = new[]
            {
                "All Mips", "Mip 0", "Mip 1", "Mip 2", "Mip 3", "Mip 4",
                "Mip 5", "Mip 6", "Mip 7", "Mip 8", "Mip 9", "Mip 10",
            };
            var stored = property.intValue;
            if (StoredValueIsOutOfBand(stored, LowestMipCap, HighestMipCap))
            {
                EditorGUILayout.HelpBox(
                    OutOfBandWarning(content.text, stored,
                        Mathf.Clamp(stored, LowestMipCap, HighestMipCap)),
                    MessageType.Warning);
            }
            var index = stored < 0
                ? 0
                : Mathf.Min(stored + 1, options.Length - 1);
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.Popup(
                content,
                index,
                options);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = selected == 0 ? -1 : selected - 1;
            }
        }

        private void DrawMinTextureSizePopup(
            string propertyPath, GUIContent content)
        {
            var sizeProperty = serializedObject.FindProperty(propertyPath);

            // Index 0 is "All Sizes" (stored -1); index i maps to 2^i.
            var sizeOptions = new string[14];
            sizeOptions[0] = "All Sizes";
            for (var option = 1; option < sizeOptions.Length; option++)
            {
                sizeOptions[option] = Mathf.RoundToInt(
                    Mathf.Pow(2, option)).ToString();
            }

            var storedSize = sizeProperty.intValue;
            if (TextureSizeIsOutOfBand(storedSize))
            {
                EditorGUILayout.HelpBox(
                    OutOfBandWarning(content.text,
                        storedSize,
                        NearestLegalTextureSize(storedSize)),
                    MessageType.Warning);
            }
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

            EditorGUI.BeginChangeCheck();
            var selectedSize = EditorGUILayout.Popup(
                content,
                sizeIndex,
                sizeOptions);
            if (EditorGUI.EndChangeCheck())
            {
                sizeProperty.intValue = selectedSize == 0
                    ? -1
                    : Mathf.RoundToInt(Mathf.Pow(2, selectedSize));
            }
        }

        private int PercentSlider(string propertyPath, GUIContent content)
        {
            var property = serializedObject.FindProperty(propertyPath);
            var stored = property.intValue;
            if (StoredValueIsOutOfBand(stored, LowestPercent, HighestPercent))
            {
                EditorGUILayout.HelpBox(
                    OutOfBandWarning(content.text, stored,
                        Mathf.Clamp(stored, LowestPercent, HighestPercent)),
                    MessageType.Warning);
            }
            var drawn = Mathf.Clamp(stored, LowestPercent, HighestPercent);
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.IntSlider(
                content,
                drawn,
                LowestPercent, HighestPercent);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = selected;
                return selected;
            }
            return drawn;
        }

        private void DrawCoverageSlider()
        {
            PercentSlider("_minimumOpaqueCoveragePercent",
                MaterialCoverageRowContent);
        }

        private void DrawAlphaPolicyControls()
        {
            // The clamp normalization is deliberate policy, not an
            // out-of-band rewrite. It runs only when the user moved a
            // slider on this repaint.
            EditorGUI.BeginChangeCheck();
            var minimumOpaqueAlpha = PercentSlider(
                "_minimumOpaqueAlphaPercent", TextureClampRowContent);
            PercentSlider("_polygonMinimumOpaqueCoveragePercent",
                PolygonCoverageRowContent);
            var polygonClamp = PercentSlider(
                "_polygonAlphaUpperClampPercent", PolygonClampRowContent);
            if (EditorGUI.EndChangeCheck())
            {
                var clampProperty = serializedObject.FindProperty(
                    "_polygonAlphaUpperClampPercent");
                clampProperty.intValue = NormalizePolygonClamp(
                    minimumOpaqueAlpha, polygonClamp);
            }
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

        private const int LowestMipCap = -1;
        private const int HighestMipCap = 10;
        private const int SmallestTextureSize = 2;
        private const int LargestTextureSize = 8192;
        private const int LowestPercent = 0;
        private const int HighestPercent = 100;

        /// <summary>
        /// True when a stored value sits outside the legal band of
        /// its control. The inspector never pulls a stored value back
        /// into the band by itself. It draws a warning instead and
        /// writes only when the user changes the control. The tests
        /// assert this predicate directly.
        /// </summary>
        internal static bool StoredValueIsOutOfBand(
            int stored, int lowestLegal, int highestLegal)
        {
            return stored < lowestLegal || stored > highestLegal;
        }

        /// <summary>
        /// True when a stored texture size is neither All Sizes (-1)
        /// nor a power of two between 2 and 8192. A texture size band
        /// is not one range, so this rule has its own method. The
        /// tests assert it directly.
        /// </summary>
        internal static bool TextureSizeIsOutOfBand(int stored)
        {
            if (stored == -1)
            {
                return false;
            }
            if (StoredValueIsOutOfBand(
                    stored, SmallestTextureSize, LargestTextureSize))
            {
                return true;
            }
            return (stored & (stored - 1)) != 0;
        }

        /// <summary>
        /// The nearest legal texture size for a stored value. Sizes
        /// round up to the next power of two, up to the 8192 cap. A
        /// stored value of zero or less reads as All Sizes.
        /// </summary>
        internal static int NearestLegalTextureSize(int stored)
        {
            if (stored <= 0)
            {
                return -1;
            }
            if (stored >= LargestTextureSize)
            {
                return LargestTextureSize;
            }
            var size = SmallestTextureSize;
            while (size < stored)
            {
                size *= 2;
            }
            return size;
        }

        /// <summary>
        /// One warning builder for every out-of-band control. The
        /// text names the stored value, states the nearest legal
        /// value, and states that AMUSE writes only when the user
        /// changes the control.
        /// </summary>
        internal static string OutOfBandWarning(
            string controlName, int stored, int nearestLegal)
        {
            return controlName + " holds " + stored + ". That " +
                "value is outside the legal choices. The nearest " +
                "legal value is " + nearestLegal + ". AMUSE writes " +
                "a new value only when you change this control.";
        }

        /// <summary>
        /// The Settings foldout. It holds the master switch, the reveal
        /// toggle for the advanced options, the two tolerance consents,
        /// and the reveal toggle for the alpha policy menu. Every
        /// switch a user must find lives here, so one menu answers
        /// "what does AMUSE do on this avatar". The two tolerance
        /// consents and the alpha policy menu stay hidden until
        /// Advanced Settings is on, because most users never change
        /// them. The reveal toggle stores on the component, so Reset
        /// and Undo treat it like every other control.
        /// </summary>
        private bool DrawSettings()
        {
            _settingsOpen = EditorGUILayout.Foldout(
                _settingsOpen, "Settings", EditorStyles.foldoutHeader);
            OnFoldoutHeaderDrawnForTest?.Invoke("Settings");
            if (!_settingsOpen)
            {
                return false;
            }

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_amuseDisabled"),
                new GUIContent("Disable AMUSE",
                    "Treats this component as absent: nothing runs on " +
                    "build, in Play mode, or anywhere else, and nothing " +
                    "is reported."));

            var reveal = serializedObject.FindProperty(
                "_advancedSettingsRevealed");
            EditorGUILayout.PropertyField(
                reveal,
                new GUIContent("Advanced Settings",
                    "Shows the two tolerance consents and the Alpha " +
                    "Separator Settings menu below. Most users never " +
                    "need them."));

            if (reveal.boolValue)
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(
                        "_ignoreOutOfRangeMaterialSlots"),
                    new GUIContent(
                        "Ignore Out-of-Range Material Slots",
                        "Some animation files animate material slots that do not exist on this mesh. " +
                        "By default, AMUSE safely ignores these extra slot animations and optimizes the valid slots. " +
                        "Turn this setting off to refuse meshes with extra slot animations."));

                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("_allowDepthTestChange"),
                    new GUIContent(
                        "Allow Depth Test Change on Moved Triangles",
                        "Some materials set a special depth rule: draw a " +
                        "pixel only when it is strictly closer than " +
                        "everything already drawn. When AMUSE moves solid " +
                        "triangles of such a material onto an opaque copy, " +
                        "those triangles use the normal depth rule, which " +
                        "also draws pixels at the same distance. On rare " +
                        "layered parts, surfaces at exactly the same " +
                        "distance can swap their draw order or flicker." +
                        "\n\n" +
                        "By default, AMUSE accepts this stated change and " +
                        "moves the triangles. Turn this setting off to keep " +
                        "materials with a special depth rule on their " +
                        "original material."));
            }

            serializedObject.ApplyModifiedProperties();
            return reveal.boolValue;
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

            var subtextStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                wordWrap = true,
            };
            // A proper product name, so the plain-English sentence
            // rules do not rewrite it.
            EditorGUILayout.LabelField(
                "Alrauna's Material Understanding and Simplification Engine",
                subtextStyle);

            if (GUILayout.Button("Report a bug"))
            {
                Application.OpenURL("https://github.com/Alrauna/AMUSE/issues");
            }

            EditorGUILayout.Space(4);
        }
    }
}