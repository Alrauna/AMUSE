using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Loads the three shipped preset files from the installed package.
    /// The file names are fixed, so "safe" always means safe.json. The
    /// folder resolves from the package metadata, the same pattern the
    /// header uses for the version, so no path is ever built by hand.
    /// </summary>
    internal static class PresetFileStore
    {
        internal static readonly string[] FileNames =
            { "safe", "normal", "aggressive" };

        internal static bool TryLoadAll(
            out List<OptimizerPreset> presets,
            out string failedFile,
            out PresetLoadRefusal refusal)
        {
            presets = new List<OptimizerPreset>();
            failedFile = null;
            refusal = PresetLoadRefusal.None;

            var folder = PresetsFolder();
            if (folder == null)
            {
                failedFile = "Presets";
                refusal = PresetLoadRefusal.FileMissing;
                return false;
            }

            foreach (var name in FileNames)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    folder + "/" + name + ".json");
                if (asset == null)
                {
                    failedFile = name + ".json";
                    refusal = PresetLoadRefusal.FileMissing;
                    return false;
                }
                if (!PresetParser.TryParse(
                        asset.text, out var preset, out refusal))
                {
                    failedFile = name + ".json";
                    return false;
                }
                presets.Add(preset);
            }
            return true;
        }

        private static string PresetsFolder()
        {
            var info = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(PresetFileStore).Assembly);
            return info == null ? null : info.assetPath + "/Presets";
        }
    }
}
