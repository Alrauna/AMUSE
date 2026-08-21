using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// Which AMUSE shader frontend attests a material, or none.
    /// <para>
    /// The two families the census measures are named here, directly, and the
    /// compiler checks them. Nothing is discovered: an earlier draft reflected a
    /// namespace for a method called <c>AnalyzeBaseMaterial</c>, which made the
    /// census's own vocabulary depend on AMUSE's folder and naming conventions,
    /// so a rename that changed nothing semantically could silently change what
    /// was measured. <c>CensusVocabularyTests</c> pins the frontend set instead.
    /// </para>
    /// <para>
    /// AMUSE's own selector runs this same exclusive trial and then discards
    /// which frontend answered, and the census's highest-value number is exactly
    /// that answer. Repeating the trial here costs a second attestation per
    /// distinct material; changing AMUSE to report it would be a production
    /// result-object change made solely so the census could measure something,
    /// which the harness design forbids.
    /// </para>
    /// <para>
    /// Attestation hashes the whole normalized shader source, and avatars repeat
    /// material references across slots and renderers, so the memo removes real
    /// repeated work. It is scoped to one collection run and discarded with it;
    /// it holds material references and must not outlive the run.
    /// </para>
    /// </summary>
    internal sealed class CensusShaderFamily
    {
        private readonly Dictionary<Material, Census.ShaderFamilyAttestation>
            _memo = new Dictionary<Material, Census.ShaderFamilyAttestation>();

        internal Census.ShaderFamilyAttestation Of(Material material)
        {
            // Unity's overloaded equality reports a destroyed object as null.
            // Both frontends throw on a shaderless material, and the answer for
            // one is None either way.
            if (material == null || material.shader == null)
            {
                return Census.ShaderFamilyAttestation.None;
            }

            if (_memo.TryGetValue(material, out var cached))
            {
                return cached;
            }

            // AMUSE's own trial order, restated. No material can be attested by
            // both frontends, so the order affects cost, not the answer.
            var family = Census.ShaderFamilyAttestation.None;
            if (PoiyomiMaterialSemantics
                .AnalyzeBaseMaterial(material).IsSupportedMaterial)
            {
                family = Census.ShaderFamilyAttestation.Poiyomi;
            }
            else if (LilToonMaterialSemantics
                     .AnalyzeBaseMaterial(material).IsSupportedMaterial)
            {
                family = Census.ShaderFamilyAttestation.LilToon;
            }

            _memo[material] = family;
            return family;
        }
    }
}
