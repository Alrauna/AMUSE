using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The lock condition evaluator ports the vendor condition grammar of
    /// spec fact 9 over the accepted forms of spec fact 10. A removed block
    /// must stay removed on the unlocked material, so isNotAnimated counts
    /// as true here (A6.2). Every fixture uses synthetic property names and
    /// holds no vendor text.
    /// </summary>
    public sealed class PoiyomiLockConditionTests
    {
        /// <summary>
        /// Plain property numbers that the tests control. The default queue
        /// of 2000 is the Unity opaque queue. No Unity object takes part.
        /// </summary>
        private sealed class SyntheticValues : IPoiyomiLockConditionValues
        {
            private readonly Dictionary<string, float> numbers =
                new Dictionary<string, float>();
            private readonly HashSet<string> textures = new HashSet<string>();

            internal SyntheticValues WithNumber(string name, float value)
            {
                numbers[name] = value;
                return this;
            }

            internal SyntheticValues WithTexture(string name)
            {
                textures.Add(name);
                return this;
            }

            internal SyntheticValues WithQueue(int queue)
            {
                RenderQueue = queue;
                return this;
            }

            public int RenderQueue { get; private set; } = 2000;

            public bool TryGetNumber(string propertyName, out float value)
            {
                return numbers.TryGetValue(propertyName, out value);
            }

            public bool IsTexture(string propertyName)
            {
                return textures.Contains(propertyName);
            }
        }

        /// <summary>
        /// An equality that matches the stored value holds. The same text
        /// with a different value does not hold. Falsifies an evaluator
        /// that hardcodes one verdict per comparison.
        /// </summary>
        [Test]
        public void EqualityComparisonIsTrueWhenTheValueMatches()
        {
            var matching = Evaluate("_A==1", new SyntheticValues().WithNumber("_A", 1f));
            Assert.That(matching, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "The equality holds at the stored value.");

            var differing = Evaluate("_A==1", new SyntheticValues().WithNumber("_A", 0f));
            Assert.That(differing, Is.EqualTo(PoiyomiLockConditionOutcome.False),
                "The equality fails at another value.");
        }

        /// <summary>
        /// Inequality reads the stored value the same way. Falsifies an
        /// evaluator that flips the whole verdict instead of the comparison.
        /// </summary>
        [Test]
        public void InequalityComparisonUsesTheValue()
        {
            var outcome = Evaluate("_A!=1", new SyntheticValues().WithNumber("_A", 2f));
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "Two and one differ, so the inequality holds.");
        }

        /// <summary>
        /// Spaces around the operators and the join are whitespace, not
        /// syntax. Both comparisons read their values. Falsifies a tokenizer
        /// that keeps the spaces inside an operand name.
        /// </summary>
        [Test]
        public void SpacesAroundOperatorsAreAccepted()
        {
            var values = new SyntheticValues()
                .WithNumber("_A", 0f)
                .WithNumber("_B", 0f);
            var outcome = Evaluate("_A == 0 && _B == 0", values);
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "Both spaced comparisons hold at zero.");
        }

        /// <summary>
        /// isNotAnimated counts as true, because the vendor removal proves
        /// the clause held at lock time (A6.2). Falsifies an evaluator that
        /// needs real animation data to answer the clause.
        /// </summary>
        [Test]
        public void IsNotAnimatedCountsAsTrue()
        {
            var values = new SyntheticValues().WithNumber("_A", 0f);
            var outcome = Evaluate("_A==0 && isNotAnimated(_A)", values);
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "Both the comparison and the isNotAnimated clause hold.");
        }

        /// <summary>
        /// RenderQueue reads the render queue of the values surface. The
        /// same text flips when the queue crosses 3000. Falsifies an
        /// evaluator that treats RenderQueue as an unknown property of 0.
        /// </summary>
        [Test]
        public void RenderQueueReadsTheQueue()
        {
            var below = Evaluate("RenderQueue>3000", new SyntheticValues().WithQueue(2000));
            Assert.That(below, Is.EqualTo(PoiyomiLockConditionOutcome.False),
                "The queue of 2000 is not past 3000.");

            var above = Evaluate("RenderQueue>3000", new SyntheticValues().WithQueue(3001));
            Assert.That(above, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "The queue of 3001 is past 3000.");
        }

        /// <summary>
        /// The constant form 0==0 holds with no values at all. Falsifies an
        /// evaluator that demands a declared property on either side.
        /// </summary>
        [Test]
        public void ConstantComparisonIsTrue()
        {
            var outcome = Evaluate("0==0", new SyntheticValues());
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "Zero equals zero with no property involved.");
        }

        /// <summary>
        /// One admitted condition carries trailing text after the number.
        /// The number prefix parses and the rest is ignored, so the text
        /// means its plain comparison (fact 10). Falsifies an evaluator
        /// that feeds the whole tail into the number parse and refuses.
        /// </summary>
        [Test]
        public void TrailingTextAfterTheNumberReadsAsAMissingProperty()
        {
            var text = "_A==0// [Vector2] _B (\"x\", Vector) = (1,2,0,0)";
            var holding = Evaluate(text, new SyntheticValues().WithNumber("_A", 0f));
            Assert.That(holding, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "The comparison before the trailing text holds at zero.");

            var failing = Evaluate(text, new SyntheticValues().WithNumber("_A", 1f));
            Assert.That(failing, Is.EqualTo(PoiyomiLockConditionOutcome.False),
                "The comparison before the trailing text fails at one.");
        }

        /// <summary>
        /// The grammar splits at the first top level join and never applies
        /// precedence. The right side of the first join keeps the second
        /// join, so the text reads as A or (B and C). The vendor answers
        /// true here. A precedence-correct left grouping reads false, so
        /// this test catches that wrong implementation.
        /// </summary>
        [Test]
        public void FirstTopLevelOperatorSplitsWithoutPrecedence()
        {
            var values = new SyntheticValues()
                .WithNumber("_A", 1f)
                .WithNumber("_B", 0f)
                .WithNumber("_C", 0f);
            var outcome = Evaluate("_A==1 || _B==1 && _C==1", values);
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "The first join groups the right side, so the or holds.");
        }

        /// <summary>
        /// A texture operand makes a numeric comparison false, because a
        /// texture has no number to compare (fact 9). Falsifies an
        /// evaluator that reads a texture as a missing property of 0 and
        /// answers true.
        /// </summary>
        [Test]
        public void TextureOperandComparesFalse()
        {
            var values = new SyntheticValues().WithTexture("_T");
            var outcome = Evaluate("_T==0", values);
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.False),
                "A texture operand never compares equal to a number.");
        }

        /// <summary>
        /// The vendor version operands sit outside the accepted forms, so
        /// the verdict is Unsupported and the caller refuses (A6.2).
        /// Falsifies an evaluator that reads the unknown version token as 0.
        /// </summary>
        [Test]
        public void VendorVersionOperandIsUnsupported()
        {
            var outcome = Evaluate("ThryEditor>1", new SyntheticValues());
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.Unsupported),
                "A vendor version operand is outside the accepted forms.");
        }

        /// <summary>
        /// An operand that is not a declared property and not a number
        /// reads 0 (fact 9). The text compares that 0, so the verdict
        /// follows the comparison. Falsifies an evaluator that refuses an
        /// unknown identifier instead of reading it as 0.
        /// </summary>
        [Test]
        public void UnknownIdentifierReadsAsZero()
        {
            var outcome = Evaluate("_Missing==0", new SyntheticValues());
            Assert.That(outcome, Is.EqualTo(PoiyomiLockConditionOutcome.True),
                "The unknown operand reads 0 and equals the literal.");
        }

        /// <summary>
        /// The operand list serves the unused property check (A6.3). It
        /// names the operands of comparisons only. The property inside
        /// isNotAnimated is no comparison operand, so the list leaves it
        /// out. Falsifies a collector that walks function arguments too.
        /// </summary>
        [Test]
        public void ComparisonOperandsListsOnlyComparedProperties()
        {
            var operands = PoiyomiLockCondition.ComparisonOperands("_A==0 && isNotAnimated(_B)");
            Assert.That(operands, Is.EqualTo(new[] { "_A" }),
                "Only the compared property enters the list.");
        }

        private static PoiyomiLockConditionOutcome Evaluate(
            string conditionText,
            IPoiyomiLockConditionValues values)
        {
            return PoiyomiLockCondition.Evaluate(conditionText, values);
        }
    }
}
