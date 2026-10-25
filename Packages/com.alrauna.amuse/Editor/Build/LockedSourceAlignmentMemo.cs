using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The session memo for one locked source alignment (A8.4). The entry
    /// key binds the generated asset path to its write time, plus the
    /// original asset path to its text digest. A same-path relock with a
    /// new write time therefore re-aligns, and a changed original file
    /// re-aligns even when its write time did not move. Reproduction and
    /// checks are never memoized. The build reset and the tests clear the
    /// memo through <see cref="Clear"/>.
    /// </summary>
    internal static class LockedSourceAlignmentMemo
    {
        private const string UnreadableDetail =
            "The locked or the original shader file is not readable.";

        private sealed class Entry
        {
            internal string Key;
            internal string GeneratedStamp;
            internal LockedSourceAlignmentResult Result;
        }

        private static readonly Dictionary<string, Entry> Entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>
        /// Returns the aligned facts of the pair, aligned again only when
        /// either file changed since the last call (A8.4). A refusal is
        /// memoized like a success, because the key already covers every
        /// input of the alignment.
        /// </summary>
        internal static LockedSourceAlignmentResult GetOrAlign(
            Shader generated,
            Shader original,
            IReadOnlyList<LockedSourceProperty> properties)
        {
            if (!TryReadPair(generated, original,
                    out var generatedPath, out var generatedText,
                    out var originalPath, out var originalText,
                    out var refusal))
            {
                return refusal;
            }

            var stamp = File.GetLastWriteTimeUtc(generatedPath)
                .Ticks.ToString(CultureInfo.InvariantCulture);
            var key = generatedPath
                + "\u001f" + stamp
                + "\u001f" + originalPath
                + "\u001f" + DigestOf(originalText);
            if (Entries.TryGetValue(generatedPath, out var entry) &&
                entry.Key == key)
            {
                return entry.Result;
            }

            var result = PoiyomiLockedSourceAlignment.Align(
                originalText, generatedText, properties);
            Entries[generatedPath] = new Entry
            {
                Key = key,
                GeneratedStamp = stamp,
                Result = result,
            };
            return result;
        }

        /// <summary>
        /// Returns the aligned facts the memo holds for one generated
        /// shader, or null when no current entry exists. The check runs
        /// A5.9 and A6 again on the finished clone from these facts
        /// (A8.3).
        /// </summary>
        internal static AlignedLockedSource Peek(Shader generated)
        {
            if (generated == null)
            {
                return null;
            }

            var generatedPath = AssetDatabase.GetAssetPath(generated);
            if (string.IsNullOrEmpty(generatedPath) ||
                !Entries.TryGetValue(generatedPath, out var entry))
            {
                return null;
            }

            var stamp = File.GetLastWriteTimeUtc(generatedPath)
                .Ticks.ToString(CultureInfo.InvariantCulture);
            if (entry.GeneratedStamp != stamp)
            {
                return null;
            }

            return entry.Result.Succeeded ? entry.Result.Source : null;
        }

        /// <summary>
        /// Drops every memoized alignment. The build reset and the tests
        /// call this (A8.4).
        /// </summary>
        internal static void Clear()
        {
            Entries.Clear();
        }

        /// <summary>
        /// The original property table that the aligner reads (A3.1):
        /// names, types, float defaults, and the HDR flag from the shader's
        /// own property walk. The Gamma flag exists only in the original
        /// text, so it comes from the aligner's attribute walk.
        /// </summary>
        internal static IReadOnlyList<LockedSourceProperty> PropertiesOf(
            Shader original,
            string originalText)
        {
            if (original == null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            if (originalText == null)
            {
                throw new ArgumentNullException(nameof(originalText));
            }

            var gammaNames = PoiyomiLockedSourceAlignment
                .GammaPropertyNames(originalText);
            var properties = new List<LockedSourceProperty>(
                original.GetPropertyCount());
            var count = original.GetPropertyCount();
            for (var index = 0; index < count; index++)
            {
                var name = original.GetPropertyName(index);
                var type = original.GetPropertyType(index);
                var flags = original.GetPropertyFlags(index);
                float defaultFloat;
                if (type == ShaderPropertyType.Int)
                {
                    defaultFloat = original.GetPropertyDefaultIntValue(
                        index);
                }
                else if (type == ShaderPropertyType.Float ||
                    type == ShaderPropertyType.Range)
                {
                    defaultFloat = original.GetPropertyDefaultFloatValue(
                        index);
                }
                else
                {
                    defaultFloat = 0f;
                }

                properties.Add(new LockedSourceProperty(
                    name,
                    type,
                    (flags & ShaderPropertyFlags.HDR) != 0,
                    gammaNames.Contains(name),
                    defaultFloat));
            }

            return properties;
        }

        private static bool TryReadPair(
            Shader generated,
            Shader original,
            out string generatedPath,
            out string generatedText,
            out string originalPath,
            out string originalText,
            out LockedSourceAlignmentResult refusal)
        {
            generatedPath = null;
            generatedText = null;
            originalPath = null;
            originalText = null;
            refusal = default;
            if (generated == null || original == null)
            {
                refusal = new LockedSourceAlignmentResult(
                    false, null, UnreadableDetail, 0);
                return false;
            }

            generatedPath = AssetDatabase.GetAssetPath(generated);
            originalPath = AssetDatabase.GetAssetPath(original);
            generatedText = LockedMaterialIdentity.ReadOriginalText(
                generated);
            originalText = LockedMaterialIdentity.ReadOriginalText(
                original);
            if (string.IsNullOrEmpty(generatedPath) ||
                string.IsNullOrEmpty(originalPath) ||
                generatedText == null ||
                originalText == null)
            {
                refusal = new LockedSourceAlignmentResult(
                    false, null, UnreadableDetail, 0);
                return false;
            }

            return true;
        }

        private static string DigestOf(string text)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(hash)
                .Replace("-", string.Empty);
        }
    }
}
