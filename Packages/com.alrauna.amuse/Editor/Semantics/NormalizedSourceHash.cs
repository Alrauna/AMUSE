using System;
using System.Security.Cryptography;
using System.Text;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Normalizes shader source (drops an optional leading UTF-8 BOM, then
    /// converts CRLF and lone CR to LF) and returns the lowercase-hex SHA-256
    /// of its UTF-8 bytes. Pinning the normalization accepts the same
    /// official source across line-ending changes while still rejecting any
    /// content edit. Shared by the lilToon and Poiyomi source attestations,
    /// so the two pinned digests are comparable rules.
    /// </summary>
    internal static class NormalizedSourceHash
    {
        internal static string Compute(string rawSource)
        {
            if (rawSource == null)
            {
                throw new ArgumentNullException(nameof(rawSource));
            }

            if (rawSource.Length > 0 && rawSource[0] == '﻿')
            {
                rawSource = rawSource.Substring(1);
            }

            rawSource = rawSource
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            var bytes = new UTF8Encoding(false).GetBytes(rawSource);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
