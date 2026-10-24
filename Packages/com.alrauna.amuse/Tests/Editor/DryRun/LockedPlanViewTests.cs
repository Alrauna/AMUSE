using System.Collections.Generic;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.DryRun
{
    /// <summary>
    /// The plan view of the dry-run mirror. The view reads the locked
    /// values through the plan, so the consistency check sees the values
    /// the finished clone would hold. The stub values are plain records,
    /// so the tests never touch a Unity object.
    /// </summary>
    public sealed class LockedPlanViewTests
    {
        // --- Falsifier: a view that reads the locked values without the
        // plan hides every plan write from the consistency check ---
        [Test]
        public void PlanWritesCoverTheLockedValues()
        {
            var locked = new StubValues();
            locked.Floats["_LiveFloat"] = 1f;
            locked.Vectors["_LiveVector"] = new Vector4(1f, 2f, 3f, 4f);
            locked.Colors["_LiveColor"] =
                new Color(0.1f, 0.2f, 0.3f, 0.4f);
            locked.Textures["_LiveTexture"] = new LockedTextureValue(
                "token_live", 8, 8,
                new Vector2(1f, 1f), new Vector2(0f, 0f));

            var textureWrite = new LockedTextureValue(
                "token_baked", 4, 4,
                new Vector2(1f, 1f), new Vector2(0f, 0f));
            var plan = new LockedReproductionPlan(
                new Dictionary<string, float> { ["_BakedFloat"] = 0.5f },
                new Dictionary<string, Vector4>
                {
                    ["_BakedVector"] = new Vector4(4f, 3f, 2f, 1f)
                },
                new Dictionary<string, Color>
                {
                    ["_BakedColor"] = new Color(1f, 0f, 0f, 1f)
                },
                new Dictionary<string, Vector4>
                {
                    // The scale and offset write rides the plain
                    // texture name in Unity ST order (A5.4).
                    ["_LiveTexture"] =
                        new Vector4(2f, 3f, 0.25f, 0.5f)
                },
                new Dictionary<string, LockedTextureValue>
                {
                    ["_BakedTexture"] = textureWrite
                },
                new[] { "KEY_ONE" },
                2500,
                "Transparent",
                "box");

            var view = new LockedPlanView(locked, plan);

            Assert.That(view.TryGetFloat("_BakedFloat", out var baked),
                Is.True, "the plan float must read through the view");
            Assert.That(baked, Is.EqualTo(0.5f));
            Assert.That(view.TryGetVector("_BakedVector", out var vector),
                Is.True, "the plan vector must read through the view");
            Assert.That(vector, Is.EqualTo(new Vector4(4f, 3f, 2f, 1f)));
            Assert.That(view.TryGetColor("_BakedColor", out var color),
                Is.True, "the plan color must read through the view");
            Assert.That(color, Is.EqualTo(new Color(1f, 0f, 0f, 1f)));
            Assert.That(
                view.TryGetTexture("_BakedTexture", out var written),
                Is.True, "the plan texture must read through the view");
            Assert.That(written.IdentityToken, Is.EqualTo("token_baked"));
            Assert.That(
                view.TryGetTexture("_LiveTexture", out var moved),
                Is.True, "the moved texture must read through the view");
            Assert.That(moved.IdentityToken, Is.EqualTo("token_live"),
                "the texture token stays the locked one when the plan "
                + "moves only the scale and offset");
            Assert.That(moved.Scale, Is.EqualTo(new Vector2(2f, 3f)),
                "the plan scale must win under the plain name");
            Assert.That(moved.Offset, Is.EqualTo(new Vector2(0.25f, 0.5f)),
                "the plan offset must win under the plain name");
            Assert.That(view.RenderQueue, Is.EqualTo(2500));
            Assert.That(view.RenderTypeTag, Is.EqualTo("Transparent"));
            Assert.That(view.VrcFallbackTag, Is.EqualTo("box"));
            Assert.That(view.Keywords, Is.EqualTo(new[] { "KEY_ONE" }));
        }

        [Test]
        public void ReadsOutsideThePlanFallThroughToTheLockedValues()
        {
            var locked = new StubValues();
            locked.Floats["_LiveFloat"] = 1f;
            locked.Vectors["_LiveVector"] = new Vector4(1f, 2f, 3f, 4f);
            locked.Colors["_LiveColor"] =
                new Color(0.1f, 0.2f, 0.3f, 0.4f);
            locked.Textures["_LiveTexture"] = new LockedTextureValue(
                "token_live", 8, 8,
                new Vector2(1f, 1f), new Vector2(0f, 0f));
            locked.OriginalKeywords = new[] { "TAG_ONE" };
            locked.IsLinear = true;

            var plan = new LockedReproductionPlan(
                new Dictionary<string, float>(),
                new Dictionary<string, Vector4>(),
                new Dictionary<string, Color>(),
                new Dictionary<string, Vector4>(),
                new Dictionary<string, LockedTextureValue>(),
                new[] { "KEY_ONE" },
                2000,
                null,
                null);

            var view = new LockedPlanView(locked, plan);

            Assert.That(view.TryGetFloat("_LiveFloat", out var floatValue),
                Is.True);
            Assert.That(floatValue, Is.EqualTo(1f));
            Assert.That(
                view.TryGetVector("_LiveVector", out var vectorValue),
                Is.True);
            Assert.That(vectorValue, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            Assert.That(
                view.TryGetColor("_LiveColor", out var colorValue), Is.True);
            Assert.That(colorValue,
                Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.That(
                view.TryGetTexture("_LiveTexture", out var textureValue),
                Is.True);
            Assert.That(textureValue.IdentityToken, Is.EqualTo("token_live"));
            Assert.That(textureValue.Scale, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(textureValue.Offset,
                Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(view.TryGetFloat("_Missing", out _), Is.False,
                "a name the locked view refuses stays refused");
            Assert.That(view.OriginalKeywordsTag,
                Is.EqualTo(new[] { "TAG_ONE" }),
                "the tag keywords stay a locked-side fact");
            Assert.That(view.ProjectIsLinear, Is.True,
                "the project color space stays a locked-side fact");
        }

        private sealed class StubValues : ILockedMaterialValues
        {
            internal readonly Dictionary<string, float> Floats =
                new Dictionary<string, float>();

            internal readonly Dictionary<string, Vector4> Vectors =
                new Dictionary<string, Vector4>();

            internal readonly Dictionary<string, Color> Colors =
                new Dictionary<string, Color>();

            internal readonly Dictionary<string, LockedTextureValue>
                Textures = new Dictionary<string, LockedTextureValue>();

            internal string[] OriginalKeywords = System.Array.Empty<string>();

            internal bool IsLinear;

            public bool TryGetFloat(string propertyName, out float value)
            {
                return Floats.TryGetValue(propertyName, out value);
            }

            public bool TryGetVector(
                string propertyName, out Vector4 value)
            {
                return Vectors.TryGetValue(propertyName, out value);
            }

            public bool TryGetColor(string propertyName, out Color value)
            {
                return Colors.TryGetValue(propertyName, out value);
            }

            public bool TryGetTexture(
                string propertyName, out LockedTextureValue value)
            {
                return Textures.TryGetValue(propertyName, out value);
            }

            public int RenderQueue => 0;

            public string RenderTypeTag => null;

            public string VrcFallbackTag => null;

            public IReadOnlyList<string> OriginalKeywordsTag =>
                OriginalKeywords;

            public IReadOnlyList<string> Keywords =>
                System.Array.Empty<string>();

            public bool ProjectIsLinear => IsLinear;
        }
    }
}
