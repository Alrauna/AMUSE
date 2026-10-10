using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Loads the three shipped preset files from the installed package.
    /// The file names are fixed, so "safe" always means safe.json. The
    /// folder resolves from the package metadata, the same pattern the
    /// header uses for the version, so no path is ever built by hand.
    /// A rooted package path is relativized against the project root
    /// when it stays inside the project. When that route holds no
    /// imported text asset, the store falls back to a direct read
    /// from disk. A failed load never answers a partially filled
    /// list, because a partial preset universe could claim a match
    /// it cannot prove.
    /// </summary>
    internal static class PresetFileStore
    {
        private static readonly string[] FileNames =
            { "safe", "normal", "aggressive" };

        internal static bool TryLoadAll(
            out List<OptimizerPreset> presets,
            out string failedFile,
            out PresetLoadRefusal refusal)
        {
            return TryLoadAllFor(
                PresetsFolder(), out presets, out failedFile,
                out refusal);
        }

        internal static bool TryLoadAllFor(
            string folder,
            out List<OptimizerPreset> presets,
            out string failedFile,
            out PresetLoadRefusal refusal)
        {
            presets = new List<OptimizerPreset>();
            failedFile = null;
            refusal = PresetLoadRefusal.None;

            if (folder == null)
            {
                failedFile = "Presets";
                refusal = PresetLoadRefusal.FileMissing;
                return false;
            }

            foreach (var name in FileNames)
            {
                string text;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    folder + "/" + name + ".json");
                if (asset != null)
                {
                    text = asset.text;
                }
                else if (Directory.Exists(folder) &&
                         File.Exists(folder + "/" + name + ".json"))
                {
                    // A folder outside the project holds no imported
                    // text assets, so the store reads the file
                    // directly from disk.
                    text = File.ReadAllText(
                        folder + "/" + name + ".json");
                }
                else
                {
                    failedFile = name + ".json";
                    refusal = PresetLoadRefusal.FileMissing;
                    presets.Clear();
                    return false;
                }
                if (!PresetParser.TryParse(
                        text, out var preset, out refusal))
                {
                    failedFile = name + ".json";
                    presets.Clear();
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
            if (info == null)
            {
                return null;
            }
            if (!Path.IsPathRooted(info.assetPath))
            {
                return info.assetPath + "/Presets";
            }

            // A rooted package path works only as a disk route. Try a
            // project-relative form first, so the imported text assets
            // answer. A path that leaves the project keeps its rooted
            // form for the direct read.
            var projectRoot = Directory
                .GetParent(Application.dataPath).FullName;
            var relative = Path.GetRelativePath(
                projectRoot, info.assetPath);
            if (relative.StartsWith(".."))
            {
                return info.assetPath + "/Presets";
            }
            return relative + "/Presets";
        }
    }
}
