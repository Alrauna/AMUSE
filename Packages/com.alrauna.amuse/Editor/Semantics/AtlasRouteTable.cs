using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// One admitted shader row's atlas route: the member texture that
    /// moves into a page, the assigned textures no reachable code
    /// samples, the local feature keywords a use may hold, the
    /// condition properties no animation or property block may touch,
    /// and the required values a material must hold.
    /// <para>
    /// The keyword list is an allowed list, so it is complete by
    /// construction. A keyword that is not on the list refuses the use.
    /// A feature that a review missed therefore cannot pass. The source
    /// review of 2026-11-19 fixed the entries against the pinned shader
    /// digests, so a pinned row cannot gain an unlisted feature.
    /// </para>
    /// <para>
    /// <see cref="HoldsRequiredValues"/> covers the scalar required
    /// values only. The texture transform has its own capture step and
    /// its own refusal, because a moved texture with non-identity
    /// tiling needs a different hint than a wrong feature value. The
    /// transform properties are listed separately with the identity
    /// vector each must hold.
    /// </para>
    /// </summary>
    internal sealed class AtlasRouteTable
    {
        /// <summary>
        /// The Poiyomi 9.3 main color route. The four admitted rows of
        /// <see cref="Poiyomi.PoiyomiSourceAttestation"/> share it,
        /// because the release and bundled files declare the same route
        /// surface. Member: <c>_MainTex</c> on UV0. Neutral: the five
        /// vendor textures a fresh material assigns.
        /// </summary>
        internal static readonly AtlasRouteTable PoiyomiMainColor =
            new AtlasRouteTable(
                "_MainTex",
                new[]
                {
                    "_ToonRamp",
                    "_SkinLUT",
                    "_ClothDFG",
                    "_DistortionFlowTexture",
                    "_DistortionFlowTexture1",
                },
                new[]
                {
                    "BSSBLOOMFOGTYPE_HEIGHT",
                    "VIGNETTE_MASKED",
                    "_LIGHTINGMODE_FLAT",
                    "_RIMSTYLE_POIYOMI",
                    "_RIM2STYLE_POIYOMI",
                    "_STOCHASTICMODE_DELIOT_HEITZ",
                    "_STYLIZEDREFLECTIONMODE_UNITYCHAN",
                },
                new[]
                {
                    "_MainTexUV",
                    "_MainPixelMode",
                    "_MainTexStochastic",
                    "_AlphaSharpenedA2C",
                    "_AlphaToCoverage",
                    "_RenderingAOBlockerEnabled",
                    "_MainTex_ST",
                    "_MainTexPan",
                },
                new (string, Vector4)[]
                {
                    ("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f)),
                    ("_MainTexPan", new Vector4(0f, 0f, 0f, 0f)),
                },
                HoldsPoiyomiMainColorRequiredValues);

        private static readonly string[] LilToonFeatureSwitches =
        {
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_UseMain2ndTex",
            "_UseMain3rdTex",
            "_UseGlitter",
            "_UseAudioLink",
        };

        private static readonly string[] LilToonUdimDiscardRowFlags =
            UdimDiscardRowFlagNames();

        private static readonly string[] LilToonIdMaskFlags =
            IdMaskFlagNames();

        /// <summary>
        /// The lilToon main color route. The ten admitted lilToon rows
        /// share it. Member: <c>_MainTex</c> on UV0. A fresh material
        /// assigns no other texture, so the neutral list is empty.
        /// lilToon gates its features with uniform switches and declares
        /// no local feature keyword in these rows, so the allowed list
        /// is empty too.
        /// </summary>
        internal static readonly AtlasRouteTable LilToonMainColor =
            new AtlasRouteTable(
                "_MainTex",
                Array.Empty<string>(),
                Array.Empty<string>(),
                LilToonConditionProperties(),
                new (string, Vector4)[]
                {
                    ("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f)),
                    ("_MainTex_ScrollRotate", new Vector4(0f, 0f, 0f, 0f)),
                },
                HoldsLilToonMainColorRequiredValues);

        internal string MemberProperty { get; }

        internal IReadOnlyCollection<string> NeutralProperties { get; }

        internal IReadOnlyCollection<string> AllowedKeywords { get; }

        /// <summary>
        /// The properties whose value is a route condition. No clip and
        /// no property block may target one. The list holds the scalar
        /// conditions and the two transform properties.
        /// </summary>
        internal IReadOnlyCollection<string> ConditionProperties { get; }

        /// <summary>
        /// The transform properties of the member texture with the exact
        /// identity vector each must hold. The capture reads each
        /// through <see cref="Material.GetVector"/> and compares with
        /// exact equality.
        /// </summary>
        internal IReadOnlyList<(string Property, Vector4 Identity)>
            TransformProperties { get; }

        private readonly Func<Material, bool> holdsRequiredValues;

        private AtlasRouteTable(
            string memberProperty,
            string[] neutralProperties,
            string[] allowedKeywords,
            string[] conditionProperties,
            (string, Vector4)[] transformProperties,
            Func<Material, bool> holdsRequiredValues)
        {
            MemberProperty = memberProperty;
            NeutralProperties = Array.AsReadOnly(neutralProperties);
            AllowedKeywords = Array.AsReadOnly(allowedKeywords);
            ConditionProperties = Array.AsReadOnly(conditionProperties);
            TransformProperties = Array.AsReadOnly(transformProperties);
            this.holdsRequiredValues = holdsRequiredValues;
        }

        /// <summary>
        /// Whether the material holds every scalar required value of the
        /// row. Every flag compare is exact, because these are authored
        /// flags and not measured values. So a NaN flag refuses.
        /// </summary>
        internal bool HoldsRequiredValues(Material material)
        {
            return holdsRequiredValues(material);
        }

        private static bool HoldsPoiyomiMainColorRequiredValues(
            Material material)
        {
            return material.GetFloat("_MainTexUV") == 0f &&
                   material.GetFloat("_MainPixelMode") == 0f &&
                   material.GetFloat("_MainTexStochastic") == 0f &&
                   (material.GetFloat("_AlphaSharpenedA2C") == 0f ||
                    material.GetFloat("_AlphaToCoverage") == 0f) &&
                   material.GetFloat("_RenderingAOBlockerEnabled") == 0f;
        }

        /// <summary>
        /// The lilToon row demands every feature switch at zero. The
        /// dissolve mode must round to zero on every platform, so it
        /// must lie strictly between minus one half and one half. An
        /// exact half refuses, because the rounding of a half differs
        /// between platforms. The UV tile discard flags must be zero
        /// unless its UV selector is not zero, because the shader picks
        /// UV0 with an exact compare. The ID mask flags must be zero
        /// unless its source selector is at least one. The shader reads
        /// that selector as an unsigned integer, and a value between
        /// zero and one can convert to zero, which reads UV0.
        /// </summary>
        private static bool HoldsLilToonMainColorRequiredValues(
            Material material)
        {
            foreach (var property in LilToonFeatureSwitches)
            {
                if (material.GetFloat(property) != 0f)
                {
                    return false;
                }
            }

            var dissolveMode = material.GetVector("_DissolveParams").x;
            if (!(dissolveMode > -0.5f && dissolveMode < 0.5f))
            {
                return false;
            }

            // A NaN selector counts as UV0, so the check fails closed.
            var udimSelector = material.GetFloat("_UDIMDiscardUV");
            var udimMayReadUv0 =
                udimSelector == 0f || float.IsNaN(udimSelector);
            if (udimMayReadUv0 &&
                AnyFlagSet(material, LilToonUdimDiscardRowFlags))
            {
                return false;
            }

            return material.GetFloat("_IDMaskFrom") >= 1f ||
                   !AnyFlagSet(material, LilToonIdMaskFlags);
        }

        private static bool AnyFlagSet(Material material, string[] flags)
        {
            foreach (var flag in flags)
            {
                if (material.GetFloat(flag) != 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] UdimDiscardRowFlagNames()
        {
            var names = new string[16];
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    names[row * 4 + column] =
                        "_UDIMDiscardRow" + row + "_" + column;
                }
            }

            return names;
        }

        private static string[] IdMaskFlagNames()
        {
            var names = new string[16];
            for (var index = 0; index < 8; index++)
            {
                names[index * 2] = "_IDMask" + (index + 1);
                names[index * 2 + 1] = "_IDMaskPrior" + (index + 1);
            }

            return names;
        }

        /// <summary>
        /// Every lilToon condition property: the feature switches, the
        /// dissolve parameters, both selectors with their flags, and
        /// the two transform properties.
        /// </summary>
        private static string[] LilToonConditionProperties()
        {
            var names = new List<string>(LilToonFeatureSwitches)
            {
                "_DissolveParams",
                "_UDIMDiscardUV",
                "_IDMaskFrom",
                "_MainTex_ST",
                "_MainTex_ScrollRotate",
            };
            names.AddRange(LilToonUdimDiscardRowFlags);
            names.AddRange(LilToonIdMaskFlags);
            return names.ToArray();
        }
    }
}
