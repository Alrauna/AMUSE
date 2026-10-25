using System;
using System.Collections.Generic;
using System.Globalization;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The verdict for one lock condition text. Unsupported means the text
    /// sits outside the accepted forms, so the caller refuses.
    /// </summary>
    internal enum PoiyomiLockConditionOutcome
    {
        True,
        False,
        Unsupported
    }

    /// <summary>
    /// The lock-time property numbers that a condition reads. The surface is
    /// plain strings and numbers. It never touches a Unity object.
    /// </summary>
    internal interface IPoiyomiLockConditionValues
    {
        /// <summary>The render queue of the material (fact 9).</summary>
        int RenderQueue { get; }

        /// <summary>True when the name is a texture slot of the material.</summary>
        bool IsTexture(string propertyName);

        /// <summary>Reads the number of a declared numeric property.</summary>
        bool TryGetNumber(string propertyName, out float value);
    }

    /// <summary>
    /// Evaluates one ifex lock condition text with the grammar of spec fact
    /// 9. The text splits at the first top-level join `&&` or `||` with no
    /// precedence. An operand must be one bare identifier or one number.
    /// A bare identifier that is not a declared property reads 0. A texture
    /// operand makes the comparison false. RenderQueue reads the render
    /// queue. isNotAnimated counts as true, because the vendor removal
    /// proves that the clause held at lock time (A6.2). Only a number may
    /// carry trailing comment text, as the one fact 10 condition with a
    /// trailing comment requires. A condition outside the accepted forms of
    /// spec fact 10 is Unsupported, so the caller refuses.
    /// </summary>
    internal static class PoiyomiLockCondition
    {
        private const string RenderQueueName = "RenderQueue";
        private const string IsNotAnimatedName = "isNotAnimated";
        private const int JoinLength = 2;

        // The vendor version operands sit outside the fact 10 forms (A6.2).
        private static readonly string[] VendorNames = { "ThryEditor", "VRCSDK" };

        /// <summary>
        /// Evaluates the whole condition text against the lock-time values.
        /// </summary>
        internal static PoiyomiLockConditionOutcome Evaluate(
            string conditionText,
            IPoiyomiLockConditionValues values)
        {
            if (conditionText == null)
            {
                throw new ArgumentNullException(nameof(conditionText));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            return EvaluateRange(conditionText, 0, conditionText.Length, values);
        }

        /// <summary>
        /// Lists the property names that comparisons read, in first order
        /// and without repeats. The list serves the unused property check
        /// (A6.3). A name inside isNotAnimated is no comparison operand, so
        /// the list leaves it out.
        /// </summary>
        internal static IReadOnlyList<string> ComparisonOperands(string conditionText)
        {
            if (conditionText == null)
            {
                throw new ArgumentNullException(nameof(conditionText));
            }

            var names = new List<string>();
            CollectComparisonOperands(conditionText, 0, conditionText.Length, names);
            return names;
        }

        private static PoiyomiLockConditionOutcome EvaluateRange(
            string text,
            int start,
            int end,
            IPoiyomiLockConditionValues values)
        {
            var join = TopLevelJoinIndex(text, start, end, out var isAnd);
            if (join < 0)
            {
                return EvaluateLeaf(text, start, end, values);
            }

            var left = EvaluateRange(text, start, join, values);
            var right = EvaluateRange(text, join + JoinLength, end, values);

            // One unsupported clause makes the whole verdict unsupported.
            // The caller refuses, so the join fails closed (A6.2).
            if (left == PoiyomiLockConditionOutcome.Unsupported ||
                right == PoiyomiLockConditionOutcome.Unsupported)
            {
                return PoiyomiLockConditionOutcome.Unsupported;
            }

            if (isAnd)
            {
                return Verdict(left == PoiyomiLockConditionOutcome.True &&
                    right == PoiyomiLockConditionOutcome.True);
            }

            return Verdict(left == PoiyomiLockConditionOutcome.True ||
                right == PoiyomiLockConditionOutcome.True);
        }

        /// <summary>
        /// Evaluates one join-free part of the condition. The accepted leaf
        /// forms are the function call isNotAnimated(P) and one comparison.
        /// </summary>
        private static PoiyomiLockConditionOutcome EvaluateLeaf(
            string text,
            int start,
            int end,
            IPoiyomiLockConditionValues values)
        {
            TrimRange(text, ref start, ref end);
            if (start >= end)
            {
                return PoiyomiLockConditionOutcome.Unsupported;
            }

            var nameEnd = IdentifierEnd(text, start, end);
            if (nameEnd > start && nameEnd < end && text[nameEnd] == '(')
            {
                // The clause holds, because the removal proves it held at
                // lock time (A6.2). The operand text is not read.
                if (IsName(text, start, nameEnd, IsNotAnimatedName) &&
                    EndsAtBalancedCloseParen(text, nameEnd, end))
                {
                    return PoiyomiLockConditionOutcome.True;
                }

                // Any other function, or text after the close paren, sits
                // outside the accepted forms.
                return PoiyomiLockConditionOutcome.Unsupported;
            }

            var operatorIndex = ComparisonIndex(text, start, end, out var operatorLength);
            if (operatorIndex < 0)
            {
                // No comparison operator. The leaf is outside the forms.
                return PoiyomiLockConditionOutcome.Unsupported;
            }

            var left = ResolveOperand(text, start, operatorIndex, values);
            var right = ResolveOperand(text, operatorIndex + operatorLength, end, values);
            return Compare(left, right, text, operatorIndex, operatorLength);
        }

        private static PoiyomiLockConditionOutcome Compare(
            Operand left,
            Operand right,
            string text,
            int operatorIndex,
            int operatorLength)
        {
            if (left.Kind == OperandKind.Malformed || right.Kind == OperandKind.Malformed)
            {
                return PoiyomiLockConditionOutcome.Unsupported;
            }

            // A version operand sits outside the accepted forms (A6.2).
            if (left.Kind == OperandKind.Vendor || right.Kind == OperandKind.Vendor)
            {
                return PoiyomiLockConditionOutcome.Unsupported;
            }

            // A texture operand makes a numeric comparison false (fact 9).
            if (left.Kind == OperandKind.Texture || right.Kind == OperandKind.Texture)
            {
                return PoiyomiLockConditionOutcome.False;
            }

            var a = left.Number;
            var b = right.Number;
            switch (text[operatorIndex])
            {
                case '=':
                    return Verdict(a == b);
                case '!':
                    return Verdict(a != b);
                case '>':
                    return Verdict(operatorLength == 2 ? a >= b : a > b);
                case '<':
                    return Verdict(operatorLength == 2 ? a <= b : a < b);
                default:
                    return PoiyomiLockConditionOutcome.Unsupported;
            }
        }

        private static Operand ResolveOperand(
            string text,
            int start,
            int end,
            IPoiyomiLockConditionValues values)
        {
            TrimRange(text, ref start, ref end);
            if (start >= end)
            {
                // An empty side is not an operand. The text is malformed.
                return Operand.Malformed();
            }

            var numberLength = NumberPrefixLength(text, start, end);
            if (numberLength > 0)
            {
                var remainderStart = start + numberLength;
                var isCommentTail = remainderStart + 2 <= end &&
                    text[remainderStart] == '/' &&
                    text[remainderStart + 1] == '/';
                if (remainderStart != end && !isCommentTail)
                {
                    // The number does not end the operand and the rest is
                    // no comment tail. The text sits outside the forms
                    // (A6.2).
                    return Operand.Malformed();
                }

                // Trailing text after the number is ignored (fact 10).
                var literal = text.Substring(start, numberLength);
                return Operand.FromNumber(
                    float.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture));
            }

            var nameEnd = IdentifierEnd(text, start, end);
            if (nameEnd == start || nameEnd != end)
            {
                // The operand is not one bare identifier. It is outside
                // the accepted forms, so it never falls back to a number
                // (A6.2).
                return Operand.Malformed();
            }

            if (IsName(text, start, nameEnd, RenderQueueName))
            {
                return Operand.FromNumber(values.RenderQueue);
            }

            for (var i = 0; i < VendorNames.Length; i++)
            {
                if (IsName(text, start, nameEnd, VendorNames[i]))
                {
                    return Operand.Vendor();
                }
            }

            var propertyName = text.Substring(start, nameEnd - start);
            if (values.IsTexture(propertyName))
            {
                return Operand.Texture();
            }

            if (values.TryGetNumber(propertyName, out var number))
            {
                return Operand.FromNumber(number);
            }

            // The bare identifier is not a declared property and not a
            // number. It reads 0 (fact 9).
            return Operand.Zero();
        }

        private static void CollectComparisonOperands(
            string text,
            int start,
            int end,
            List<string> names)
        {
            var join = TopLevelJoinIndex(text, start, end, out _);
            if (join >= 0)
            {
                CollectComparisonOperands(text, start, join, names);
                CollectComparisonOperands(text, join + JoinLength, end, names);
                return;
            }

            TrimRange(text, ref start, ref end);
            if (start >= end)
            {
                return;
            }

            var nameEnd = IdentifierEnd(text, start, end);
            if (nameEnd > start && nameEnd < end && text[nameEnd] == '(')
            {
                // A function call carries no comparison operands.
                return;
            }

            var operatorIndex = ComparisonIndex(text, start, end, out var operatorLength);
            if (operatorIndex < 0)
            {
                return;
            }

            AddOperandName(text, start, operatorIndex, names);
            AddOperandName(text, operatorIndex + operatorLength, end, names);
        }

        private static void AddOperandName(
            string text,
            int start,
            int end,
            List<string> names)
        {
            TrimRange(text, ref start, ref end);
            if (start >= end)
            {
                return;
            }

            if (NumberPrefixLength(text, start, end) > 0)
            {
                // A number literal names no property. Its trailing text is
                // no operand either (fact 10).
                return;
            }

            var nameEnd = IdentifierEnd(text, start, end);
            if (nameEnd == start)
            {
                return;
            }

            if (IsName(text, start, nameEnd, RenderQueueName) ||
                IsName(text, start, nameEnd, IsNotAnimatedName))
            {
                return;
            }

            for (var i = 0; i < VendorNames.Length; i++)
            {
                if (IsName(text, start, nameEnd, VendorNames[i]))
                {
                    return;
                }
            }

            var name = text.Substring(start, nameEnd - start);
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }

        /// <summary>
        /// Finds the first join at paren depth zero. The first join wins
        /// and no precedence applies (fact 9). A negative result means the
        /// range holds no top-level join.
        /// </summary>
        private static int TopLevelJoinIndex(string text, int start, int end, out bool isAnd)
        {
            var depth = 0;
            for (var i = start; i < end; i++)
            {
                var c = text[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                }
                else if (depth == 0 && i + 1 < end)
                {
                    if (c == '&' && text[i + 1] == '&')
                    {
                        isAnd = true;
                        return i;
                    }

                    if (c == '|' && text[i + 1] == '|')
                    {
                        isAnd = false;
                        return i;
                    }
                }
            }

            isAnd = false;
            return -1;
        }

        /// <summary>
        /// Finds the first comparison operator. The two-character forms win
        /// over the one-character forms at the same position. A negative
        /// result means the range holds none of the six operators.
        /// </summary>
        private static int ComparisonIndex(string text, int start, int end, out int operatorLength)
        {
            for (var i = start; i < end; i++)
            {
                var c = text[i];
                if (i + 1 < end && (c == '=' || c == '!' || c == '>' || c == '<') &&
                    text[i + 1] == '=')
                {
                    operatorLength = 2;
                    return i;
                }

                if (c == '>' || c == '<')
                {
                    operatorLength = 1;
                    return i;
                }
            }

            operatorLength = 0;
            return -1;
        }

        /// <summary>
        /// Reads the leading number shape of an operand. Returns 0 when the
        /// range does not start with a number. A lone sign is no number.
        /// </summary>
        private static int NumberPrefixLength(string text, int start, int end)
        {
            var i = start;
            if (i < end && (text[i] == '-' || text[i] == '+'))
            {
                i++;
            }

            var integerDigits = 0;
            while (i < end && IsAsciiDigit(text[i]))
            {
                i++;
                integerDigits++;
            }

            if (i < end && text[i] == '.')
            {
                var j = i + 1;
                var fractionDigits = 0;
                while (j < end && IsAsciiDigit(text[j]))
                {
                    j++;
                    fractionDigits++;
                }

                if (fractionDigits > 0)
                {
                    return j - start;
                }
            }

            return integerDigits > 0 ? i - start : 0;
        }

        /// <summary>
        /// Returns the end of the leading identifier shape. Returns the
        /// start when the range does not begin with an identifier head.
        /// </summary>
        private static int IdentifierEnd(string text, int start, int end)
        {
            if (start >= end || !IsIdentifierHead(text[start]))
            {
                return start;
            }

            var i = start + 1;
            while (i < end && IsIdentifierChar(text[i]))
            {
                i++;
            }

            return i;
        }

        private static bool IsName(string text, int start, int end, string name)
        {
            if (end - start != name.Length)
            {
                return false;
            }

            for (var i = 0; i < name.Length; i++)
            {
                if (text[start + i] != name[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True when the range after the open paren holds balanced parens
        /// and the leaf ends at its close paren. Text after the close paren
        /// fails, so a call can never hide a comparison behind it.
        /// </summary>
        private static bool EndsAtBalancedCloseParen(string text, int openIndex, int end)
        {
            // The open paren at openIndex is already consumed, so the walk
            // starts one level deep.
            var depth = 1;
            for (var i = openIndex + 1; i < end; i++)
            {
                if (text[i] == '(')
                {
                    depth++;
                }
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth < 0)
                    {
                        return false;
                    }
                }
            }

            return depth == 0 && text[end - 1] == ')';
        }

        private static void TrimRange(string text, ref int start, ref int end)
        {
            while (start < end && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(text[end - 1]))
            {
                end--;
            }
        }

        private static bool IsIdentifierHead(char c)
        {
            return c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool IsIdentifierChar(char c)
        {
            return IsIdentifierHead(c) || IsAsciiDigit(c);
        }

        private static bool IsAsciiDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static PoiyomiLockConditionOutcome Verdict(bool holds)
        {
            return holds
                ? PoiyomiLockConditionOutcome.True
                : PoiyomiLockConditionOutcome.False;
        }

        private enum OperandKind
        {
            Number,
            Texture,
            Vendor,
            Zero,
            Malformed
        }

        private readonly struct Operand
        {
            internal readonly OperandKind Kind;
            internal readonly float Number;

            private Operand(OperandKind kind, float number)
            {
                Kind = kind;
                Number = number;
            }

            internal static Operand FromNumber(float value)
            {
                return new Operand(OperandKind.Number, value);
            }

            internal static Operand Texture()
            {
                return new Operand(OperandKind.Texture, 0f);
            }

            internal static Operand Vendor()
            {
                return new Operand(OperandKind.Vendor, 0f);
            }

            internal static Operand Zero()
            {
                return new Operand(OperandKind.Zero, 0f);
            }

            internal static Operand Malformed()
            {
                return new Operand(OperandKind.Malformed, 0f);
            }
        }
    }
}
