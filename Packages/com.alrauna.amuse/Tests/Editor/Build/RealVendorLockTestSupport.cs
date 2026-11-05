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
