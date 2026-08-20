using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Walks a census record graph by reflection.
    /// <para>
    /// This exists so the privacy tests bind to the whole object graph rather
    /// than to the fields someone remembered to check. A test that enumerates
    /// known fields passes forever after a contributor adds an unknown one,
    /// which is precisely the failure the non-leakage test is supposed to
    /// catch.
    /// </para>
    /// <para>
    /// This is reflection over the research package's own types, verifying the
    /// research package's own contract. It is not the reflection-based reading
    /// of AMUSE internals the harness design rules out.
    /// </para>
    /// </summary>
    internal static class CensusReflection
    {
        /// <summary>
        /// Every string reachable from <paramref name="root"/>, through public
        /// and non-public fields and properties, and through collections.
        /// </summary>
        internal static IReadOnlyList<string> ReachableStrings(object root)
        {
            var found = new List<string>();
            Walk(root, new HashSet<object>(ReferenceEqualityComparer.Instance), found, null);
            return found;
        }

        /// <summary>
        /// A canonical rendering of the whole graph, member names sorted, for
        /// comparing two runs without depending on record equality that the
        /// records deliberately do not define.
        /// </summary>
        internal static string Describe(object root)
        {
            var builder = new StringBuilder();
            Walk(root, new HashSet<object>(ReferenceEqualityComparer.Instance), null, builder);
            return builder.ToString();
        }

        /// <summary>
        /// Every object reachable from <paramref name="root"/>, including
        /// through collections. Tier 3 uses this to prove that no per-avatar,
        /// per-renderer, or per-material record is reachable from a published
        /// report.
        /// </summary>
        internal static IReadOnlyList<object> ReachableObjects(object root)
        {
            var found = new List<object>();
            CollectObjects(
                root,
                new HashSet<object>(ReferenceEqualityComparer.Instance),
                found);
            return found;
        }

        private static void CollectObjects(
            object value,
            HashSet<object> seen,
            List<object> found)
        {
            if (value == null || value is string)
                return;

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum)
                return;
            if (!seen.Add(value))
                return;

            found.Add(value);

            if (value is IDictionary dictionary)
            {
                foreach (var key in dictionary.Keys)
                {
                    CollectObjects(key, seen, found);
                    CollectObjects(dictionary[key], seen, found);
                }

                return;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    CollectObjects(item, seen, found);
                return;
            }

            foreach (var member in Members(type))
                CollectObjects(Read(member, value), seen, found);
        }

        private static void Walk(
            object value,
            HashSet<object> seen,
            List<string> strings,
            StringBuilder description)
        {
            if (value == null)
            {
                description?.Append("null;");
                return;
            }

            if (value is string text)
            {
                strings?.Add(text);
                description?.Append('"').Append(text).Append("\";");
                return;
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                description?
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture))
                    .Append(';');
                return;
            }

            if (!seen.Add(value))
            {
                description?.Append("<seen>;");
                return;
            }

            if (value is IDictionary dictionary)
            {
                description?.Append('{');
                var keys = new List<object>();
                foreach (var key in dictionary.Keys)
                    keys.Add(key);
                keys.Sort((left, right) => string.CompareOrdinal(
                    Convert.ToString(left, CultureInfo.InvariantCulture),
                    Convert.ToString(right, CultureInfo.InvariantCulture)));

                foreach (var key in keys)
                {
                    Walk(key, seen, strings, description);
                    description?.Append("=>");
                    Walk(dictionary[key], seen, strings, description);
                }

                description?.Append("};");
                return;
            }

            if (value is IEnumerable enumerable)
            {
                description?.Append('[');
                foreach (var item in enumerable)
                    Walk(item, seen, strings, description);
                description?.Append("];");
                return;
            }

            description?.Append(type.Name).Append('(');
            foreach (var member in Members(type))
            {
                description?.Append(member.Name).Append(':');
                Walk(Read(member, value), seen, strings, description);
            }

            description?.Append(");");
        }

        private static IEnumerable<MemberInfo> Members(Type type)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var members = new List<MemberInfo>();
            foreach (var field in type.GetFields(flags))
                members.Add(field);
            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length == 0 && property.CanRead)
                    members.Add(property);
            }

            members.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return members;
        }

        private static object Read(MemberInfo member, object instance)
        {
            try
            {
                return member is FieldInfo field
                    ? field.GetValue(instance)
                    : ((PropertyInfo)member).GetValue(instance);
            }
            catch (TargetInvocationException)
            {
                return null;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance =
                new ReferenceEqualityComparer();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
