using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The shared support of the two real vendor lock suites. It holds
    /// the vendor binding constants, the assembly-qualified resolution
    /// with its binding guard, and the small text and folder helpers
    /// both suites use. Each suite keeps its own absence behavior: the
    /// characterization fails when the vendor is absent, and the
    /// acceptance suite skips with a named reason.
    /// </summary>
    internal static class RealVendorLockTestSupport
    {
        // The dirty state rule of the two preflights. A lock must never run
        // beside unsaved user work. Two kinds of dirty state are not user
        // work. The first is a native project setting file. It lives under
        // ProjectSettings, outside Assets, and a fresh import dirties it on
        // a hosted runner. The second is an untitled or preview scene, which
        // has no file for the vendor recovery pass to touch. The player
        // settings waiver that the owner approved sits inside the setting
        // file rule.
        internal static bool IsBlockingDirtyAsset(UnityEngine.Object asset)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset) ||
                !EditorUtility.IsDirty(asset) || !AssetDatabase.IsNativeAsset(asset))
                return false;
            var path = AssetDatabase.GetAssetPath(asset);
            return !path.StartsWith("ProjectSettings/", StringComparison.Ordinal);
        }

        internal static bool IsBlockingDirtyScene(UnityEngine.SceneManagement.Scene scene)
        {
            return scene.isDirty && !string.IsNullOrEmpty(scene.path) &&
                !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene);
        }

        internal static bool AnyBlockingDirtyState()
        {
            if (System.Linq.Enumerable.Any(
                    Resources.FindObjectsOfTypeAll<UnityEngine.Object>(), IsBlockingDirtyAsset))
                return true;
            for (var index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (IsBlockingDirtyScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(index)))
                    return true;
            return false;
        }

        // Names what is dirty, so a refused preflight on a runner says which
        // asset path or scene path held the guard.
        internal static string DescribeDirtyState()
        {
            var parts = new List<string>();
            foreach (var asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
                if (IsBlockingDirtyAsset(asset) && parts.Count < 8)
                    parts.Add(asset.GetType().Name + "@" + AssetDatabase.GetAssetPath(asset));
            for (var index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(index);
                if (scene.isDirty && parts.Count < 8)
                    parts.Add("scene:" + (string.IsNullOrEmpty(scene.path) ? "untitled" : scene.path)
                        + (IsBlockingDirtyScene(scene) ? "" : "(waived)"));
            }
            return "dirtyState=[" + string.Join(", ", parts) + "]";
        }

        internal const string VendorToolQualifiedName =
            "Thry.ThryEditor.ShaderOptimizer, ThryAssemblyDefinition";

        internal const string VendorToolAssemblyName =
            "ThryAssemblyDefinition";

        internal const string CacheRoot = "Assets/_LockedShaderCache";

        // The assembly-qualified resolution. It can only reach the
        // installed vendor editor assembly, because the name carries the
        // vendor assembly. A null result means the vendor package is
        // absent or its assembly name changed, and each suite decides
        // its own absence behavior on it.
        internal static Type VendorToolOrNull()
        {
            return Type.GetType(VendorToolQualifiedName, false);
        }

        // The binding guard and the member resolution. The guard asserts
        // the resolved type is non-null, its assembly is not this test
        // assembly, and its assembly name is the vendor editor assembly
        // name, so a resolution to a test local type can never pass.
        internal static void ResolveRealTool(
            Type tool,
            out MethodInfo lockMethod,
            out MethodInfo unlockMethod,
            out object progressBarNone)
        {
            Assert.That(
                tool.Assembly,
                Is.Not.EqualTo(typeof(RealVendorLockTestSupport).Assembly),
                "the resolution reached the test assembly. The suite"
                + " must bind the vendor assembly, never a test local"
                + " type");

            Assert.That(
                tool.Assembly.GetName().Name,
                Is.EqualTo(VendorToolAssemblyName),
                "the resolved assembly must be the vendor editor "
                + "assembly, got " + tool.Assembly.GetName().Name);

            var progressBarType = tool.GetNestedType("ProgressBar");
            Assert.That(
                progressBarType, Is.Not.Null,
                "the vendor nested progress bar enum must exist");

            lockMethod = tool.GetMethod(
                "LockMaterials",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(IEnumerable<Material>), progressBarType },
                null);
            unlockMethod = tool.GetMethod(
                "UnlockMaterials",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(IEnumerable<Material>), progressBarType },
                null);
            Assert.That(
                lockMethod, Is.Not.Null,
                "the vendor lock member must exist with the material "
                + "list first and the progress bar second");
            Assert.That(
                unlockMethod, Is.Not.Null,
                "the vendor unlock member must exist with the material "
                + "list first and the progress bar second");

            // The None member of the vendor nested enum, resolved
            // through Enum.Parse on the reflected type. A locally
            // declared enum would never match the vendor overload.
            progressBarNone = Enum.Parse(progressBarType, "None");
        }

        internal static void EnsureFolder(string parent, string folder)
        {
            var combined = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(combined))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        internal static string ReadText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8)
                .Replace("\r", string.Empty);
        }

        // The text of one original line for a refusal message. The
        // aligner reports the line number one based. An out of range
        // number names itself instead of throwing.
        internal static string OriginalLineText(
            string originalText, int originalLine)
        {
            var lines = originalText.Split('\n');
            if (originalLine < 1 || originalLine > lines.Length)
            {
                return "<out of range " + originalLine + ">";
            }

            var text = lines[originalLine - 1].Trim();
            return text.Length == 0
                ? "<empty line>"
                : text;
        }

        internal static string Dated(
            string detail, string runDate, string refusalDetail)
        {
            var message = detail + " on " + runDate;
            if (!string.IsNullOrEmpty(refusalDetail))
            {
                message += ": " + refusalDetail;
            }

            return message;
        }
    }
}
