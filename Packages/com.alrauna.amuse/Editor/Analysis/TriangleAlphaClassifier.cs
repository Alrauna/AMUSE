using System;
using System.Collections.Generic;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;
using TextureWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;
using BigInteger = System.Numerics.BigInteger;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal static class TriangleAlphaClassifier
    {
        internal const int MaxSupportRegions = 65536;

        // The per-candidate pre-filter translates the triangle so the
        // candidate support box sits on the square from -1 to 1. The
        // translation is exact in rationals. The conversion to doubles
        // then costs a few rounding steps of half an ulp. At magnitude
        // 256 that is about 1e-13 per coordinate. Inside the separating
        // axis test the edge vectors, the normals, and the dot products
        // stay at or below magnitude 2 to the power 19. Each rounding
        // step there costs about 1e-10. A short stack of such steps
        // stays below the 1e-9 separation margin in EdgeSeparates. So a
        // support box that touches the triangle is never reported as
        // separated. Above this bound the rounding noise can exceed the
        // margin, so the caller skips the pre-filter and lets the exact
        // intersection test decide.
        //
        // The repeat-scale walks reuse this pre-filter core unmodified, but
        // they call it directly on unwrapped boxes, so this bound never
        // caps their magnitudes. Below the bound a separated verdict is
        // trustworthy. Past it a false separation loses a witness, and the
        // no-witness arm of every classify path returns ProvenOpaque.
        private const double PreFilterSafeCoordinateBound = 256;

        /// <summary>
        /// Classifies with the per-polygon noise density policy, decoding
        /// the triangle's position validity first. The decoding overload
        /// below is the precomputed form a mip loop uses.
        /// </summary>
        internal static TriangleAlphaOutcome Classify(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            TextureSampling sampling,
            AlphaUvEnvelope envelope,
            int maxNoiseTexelPercent)
        {
            return Classify(
                triangle, texture, sampling, envelope, maxNoiseTexelPercent,
                ExactUvGeometry.DecodePositionValidity(triangle));
        }

        /// <summary>
        /// Classifies with the per-polygon noise density policy. A policy
        /// of zero keeps erasure inert. An erased texel then behaves
        /// exactly like a witness byte. A positive policy lets an erased
        /// texel skip the witness check only when the erased share of
        /// the consulted texels stays at or under the policy.
        /// <para>
        /// The position validity — vertex finiteness and the exact
        /// degeneracy verdict — is mip-invariant, so a caller classifying a
        /// chain decodes it once per triangle and passes the record here
        /// for every level. The record keeps the malformed-geometry
        /// contract: a recorded non-finite vertex still throws from this
        /// entry, exactly as the per-level validation it replaced did.
        /// </para>
        /// </summary>
        internal static TriangleAlphaOutcome Classify(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            TextureSampling sampling,
            AlphaUvEnvelope envelope,
            int maxNoiseTexelPercent,
            TrianglePositionValidity positionValidity)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            ValidateDensityPolicy(maxNoiseTexelPercent);
            if (!positionValidity.PositionsFinite)
            {
                throw new ArgumentException(
                    "Geometry positions must be finite.",
                    nameof(triangle));
            }

            if (positionValidity.IsDegenerate)
            {
                return TriangleAlphaOutcome.Unknown;
            }
            if (!triangle.HasUv0)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            ValidateFinite(triangle.Uv0, nameof(triangle.Uv0));
            ValidateFinite(triangle.Uv1, nameof(triangle.Uv1));
            ValidateFinite(triangle.Uv2, nameof(triangle.Uv2));
            if (texture.IsFullyOpaque)
            {
                return TriangleAlphaOutcome.ProvenOpaque;
            }
            if (texture.IsFullyNonOpaque)
            {
                return TriangleAlphaOutcome.MustRemainTransparent;
            }

            // Anisotropy averages an elongated footprint that no per-filter
            // envelope model bounds, so it must be decided before any filter
            // dispatch: a level that is neither fully opaque nor fully
            // non-opaque - the fast paths above answered those - stays
            // unknown, and the chain conjunction then admits an anisotropic
            // triangle only when every level is fully opaque, which is
            // exactly the anisotropic soundness condition.
            if (sampling.Aniso == TextureAnisoMode.Anisotropic)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            if (sampling.Filter == TextureFilterMode.Point &&
                sampling.Wrap == TextureWrapMode.Clamp)
            {
                return ClassifyPointClamp(triangle, texture, envelope, maxNoiseTexelPercent);
            }
            if (sampling.Filter == TextureFilterMode.Point &&
                sampling.Wrap == TextureWrapMode.Repeat)
            {
                return ClassifyPointRepeat(triangle, texture, envelope, maxNoiseTexelPercent);
            }
            if (sampling.Filter == TextureFilterMode.Bilinear &&
                sampling.Wrap == TextureWrapMode.Clamp)
            {
                return ClassifyBilinearClamp(triangle, texture, envelope, maxNoiseTexelPercent);
            }
            if (sampling.Filter == TextureFilterMode.Bilinear &&
                sampling.Wrap == TextureWrapMode.Repeat)
            {
                return ClassifyBilinearRepeat(triangle, texture, envelope, maxNoiseTexelPercent);
            }

            // Trilinear's within-level footprint is the bilinear one; the
            // between-level blend of the two adjacent selected levels is
            // monotone, and the chain conjunction in the resolver proves both
            // operands, so the bilinear per-level verdict carries over.
            if (sampling.Filter == TextureFilterMode.Trilinear &&
                sampling.Wrap == TextureWrapMode.Clamp)
            {
                return ClassifyBilinearClamp(triangle, texture, envelope, maxNoiseTexelPercent);
            }
            if (sampling.Filter == TextureFilterMode.Trilinear &&
                sampling.Wrap == TextureWrapMode.Repeat)
            {
                return ClassifyBilinearRepeat(triangle, texture, envelope, maxNoiseTexelPercent);
            }

            return TriangleAlphaOutcome.Unknown;
        }

        /// <summary>
        /// Classifies one triangle's mapped mask term saturate(r * s + v)
        /// under the same filter dispatch as the raw path, decoding the
        /// triangle's position validity first. The precomputed form below
        /// is what a mip loop uses.
        /// </summary>
        internal static TriangleAlphaOutcome ClassifyMapped(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            TextureSampling sampling,
            AlphaUvEnvelope envelope,
            AffineAlphaMap map)
        {
            return ClassifyMapped(
                triangle, texture, sampling, envelope, map,
                ExactUvGeometry.DecodePositionValidity(triangle));
        }

        /// <summary>
        /// Classifies one triangle's mapped mask term saturate(r * s + v)
        /// under the same filter dispatch as the raw path. The walk answers
        /// one question: does the triangle's contributing domain contain a
        /// texel below byte 255? The field contract then bounds the filtered
        /// red to [0, 1) on a witness and exactly 1 without one, so the map
        /// evaluated at the red bounds 0 and 1 decides the level. Mirror and
        /// MirrorOnce wrap answer Unknown like the raw dispatcher: floor
        /// division tiling names different texels than mirror reflection,
        /// so the repeat walk cannot bound a mirror footprint. Erased
        /// texels always count as witnesses: their red is unknown in [0, 1),
        /// and the raw erasure-substitution policy is a statement about the
        /// raw witness verdict, not about a red value this proof reads.
        /// <para>
        /// The position validity is mip-invariant and is decoded once per
        /// triangle by the chain caller; a recorded non-finite vertex still
        /// throws from this entry, exactly as the per-level validation it
        /// replaced did.
        /// </para>
        /// </summary>
        internal static TriangleAlphaOutcome ClassifyMapped(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            TextureSampling sampling,
            AlphaUvEnvelope envelope,
            AffineAlphaMap map,
            TrianglePositionValidity positionValidity)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            if (map.Equals(default(AffineAlphaMap)))
            {
                throw new ArgumentException(
                    "A never-constructed map cannot classify: the zero " +
                    "scale pair routes to the constant arm and FromBinary32 " +
                    "refuses it.",
                    nameof(map));
            }
            ValidateDensityPolicy(0);
            if (!positionValidity.PositionsFinite)
            {
                throw new ArgumentException(
                    "Geometry positions must be finite.",
                    nameof(triangle));
            }

            if (positionValidity.IsDegenerate)
            {
                return TriangleAlphaOutcome.Unknown;
            }
            if (!triangle.HasUv0)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            ValidateFinite(triangle.Uv0, nameof(triangle.Uv0));
            ValidateFinite(triangle.Uv1, nameof(triangle.Uv1));
            ValidateFinite(triangle.Uv2, nameof(triangle.Uv2));

            if (texture.IsFullyOpaque)
            {
                return DecideMapped(map, witness: false);
            }
            if (texture.IsFullyNonOpaque)
            {
                return DecideMapped(map, witness: true);
            }

            if (sampling.Aniso == TextureAnisoMode.Anisotropic)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            if (sampling.Filter == TextureFilterMode.Point &&
                sampling.Wrap == TextureWrapMode.Clamp)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessPointClamp(triangle, texture, envelope));
            }
            if (sampling.Filter == TextureFilterMode.Point &&
                sampling.Wrap == TextureWrapMode.Repeat)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessPointRepeat(triangle, texture, envelope));
            }
            if (sampling.Filter == TextureFilterMode.Bilinear &&
                sampling.Wrap == TextureWrapMode.Clamp)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessBilinearClamp(triangle, texture, envelope));
            }
            if (sampling.Filter == TextureFilterMode.Bilinear &&
                sampling.Wrap == TextureWrapMode.Repeat)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessBilinearRepeat(triangle, texture, envelope));
            }
            if (sampling.Filter == TextureFilterMode.Trilinear &&
                sampling.Wrap == TextureWrapMode.Clamp)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessBilinearClamp(triangle, texture, envelope));
            }
            if (sampling.Filter == TextureFilterMode.Trilinear &&
                sampling.Wrap == TextureWrapMode.Repeat)
            {
                return DecideMapped(
                    map,
                    HasMappedWitnessBilinearRepeat(triangle, texture, envelope));
            }

            return TriangleAlphaOutcome.Unknown;
        }

        /// <summary>
        /// The mapped three-way lattice on the map's endpoint envelope: a
        /// lower bound at exactly one proves the term one everywhere; an
        /// upper bound below one proves the term below one everywhere; the
        /// rest is unknown. A witness spans the red interval, and both
        /// orders are monotone in r with the direction set by the scale
        /// sign, so the witness arm envelopes both endpoint evaluations
        /// instead of assuming which endpoint holds the extreme. A
        /// no-witness red is exactly one, a single point evaluation.
        /// </summary>
        private static TriangleAlphaOutcome DecideMapped(
            AffineAlphaMap map,
            bool witness)
        {
            if (witness)
            {
                var atZero = map.Evaluate(0f);
                var atOne = map.Evaluate(1f);
                var lower = Math.Min(atZero.Lower, atOne.Lower);
                var upper = Math.Max(atZero.Upper, atOne.Upper);
                if (lower == 1f)
                {
                    return TriangleAlphaOutcome.ProvenOpaque;
                }
                if (upper < 1f)
                {
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
            }
            else
            {
                var atOne = map.Evaluate(1f);
                if (atOne.Lower == 1f)
                {
                    return TriangleAlphaOutcome.ProvenOpaque;
                }
                if (atOne.Upper < 1f)
                {
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
            }
            return TriangleAlphaOutcome.Unknown;
        }

        private static TriangleAlphaOutcome ClassifyPointClamp(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope,
            int maxNoiseTexelPercent)
        {
            var domain = ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope);
            var minimumX = PointClampIndex(
                ExactUvGeometry.Minimum(domain, true),
                texture.Width,
                domain.TexelScale);
            var maximumX = PointClampIndex(
                ExactUvGeometry.Maximum(domain, true),
                texture.Width,
                domain.TexelScale);
            var minimumY = PointClampIndex(
                ExactUvGeometry.Minimum(domain, false),
                texture.Height,
                domain.TexelScale);
            var maximumY = PointClampIndex(
                ExactUvGeometry.Maximum(domain, false),
                texture.Height,
                domain.TexelScale);
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            // The density gate decides whether erased texels act as
            // witnesses. A policy of zero keeps the plain walk: erased
            // texels stay witnesses, and no density evidence is collected.
            if (maxNoiseTexelPercent == 0)
            {
                return PointClampHasWitness(
                            texture,
                            domain,
                            false,
                            minimumX,
                            maximumX,
                            minimumY,
                            maximumY)
                    ? TriangleAlphaOutcome.MustRemainTransparent
                    : TriangleAlphaOutcome.ProvenOpaque;
            }

            // The gate is on: one walk collects the existence verdict and
            // the density evidence together. Opaque texels never witness,
            // so they stay unconsulted here. A consulted mid byte witnesses
            // under either substitution verdict, so the walk returns on it
            // at once. A consulted erased texel is only a candidate: it
            // witnesses when substitution fails, and the substitution share
            // is not known until the window has been fully walked.
            var consulted = 0L;
            var erased = 0L;
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    if (canPreFilter)
                    {
                        // A clamp edge texel's exact support interval is a
                        // half line or the whole line at width or height
                        // one, so the finite pre-filter box may not decide
                        // for it while the triangle reaches past the box
                        // edge: below zero at the first index, above the
                        // size at the last.
                        var isBoundary = texture.Width == 1 || texture.Height == 1 ||
                                         (x == 0 && (v0x < 0 || v1x < 0 || v2x < 0)) ||
                                         (x == texture.Width - 1 && (v0x > texture.Width || v1x > texture.Width || v2x > texture.Width)) ||
                                         (y == 0 && (v0y < 0 || v1y < 0 || v2y < 0)) ||
                                         (y == texture.Height - 1 && (v0y > texture.Height || v1y > texture.Height || v2y > texture.Height));
                        // The translated extraction returns false past the
                        // proven bound. Then no cheap rejection happens, and
                        // the exact intersection test below decides alone.
                        if (!isBoundary &&
                            TryExtractTexelVerticesRelativeToCandidate(
                                domain, x, y,
                                out var c0x, out var c0y,
                                out var c1x, out var c1y,
                                out var c2x, out var c2y) &&
                            !ConservativeBilinearSupportOverlapsTriangle(
                                -0.5, 0.5, -0.5, 0.5,
                                c0x, c0y, c1x, c1y, c2x, c2y))
                        {
                            continue;
                        }
                    }
                    if (!ExactUvGeometry.Intersects(
                        domain,
                        PointClampInterval(x, texture.Width, domain.TexelScale),
                        PointClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (alpha == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                        continue;
                    }
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
            }

            // Every consulted texel that reached the counters is an erased
            // candidate: the mid bytes returned above. When the collected
            // share meets the policy, the full count meets it too, because
            // the full count also consults opaque texels. No witness
            // remains, so the window proves opaque.
            if (erased * 100 <= (long)maxNoiseTexelPercent * consulted)
            {
                return TriangleAlphaOutcome.ProvenOpaque;
            }

            // The collected share exceeds the policy, so a candidate could
            // flip the verdict: the opaque consults may pull the share back
            // to or under the cap. The fallback decides with the full count
            // and the plain walk, exactly as the old two-pass did.
            var substituteErased = PointClampSubstitutesErasedTexels(
                texture,
                domain,
                maxNoiseTexelPercent,
                minimumX,
                maximumX,
                minimumY,
                maximumY);
            return PointClampHasWitness(
                        texture,
                        domain,
                        substituteErased,
                        minimumX,
                        maximumX,
                        minimumY,
                        maximumY)
                ? TriangleAlphaOutcome.MustRemainTransparent
                : TriangleAlphaOutcome.ProvenOpaque;
        }

        /// <summary>
        /// The mapped mirror of ClassifyPointClamp: the same domain
        /// construction, clamp window, budget guard, and interval
        /// intersection, but the walk only asks whether one contributing
        /// texel stores a byte below the opaque maximum. Erased texels count
        /// as witnesses: the raw substitution policy speaks to the raw
        /// witness verdict, not to a red value this proof reads. An
        /// unbounded candidate window cannot claim a bounded witness set,
        /// so it returns true - the witness bound that stays the honest
        /// unknown through the map.
        /// </summary>
        private static bool HasMappedWitnessPointClamp(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope);
            var minimumX = PointClampIndex(
                ExactUvGeometry.Minimum(domain, true),
                texture.Width,
                domain.TexelScale);
            var maximumX = PointClampIndex(
                ExactUvGeometry.Maximum(domain, true),
                texture.Width,
                domain.TexelScale);
            var minimumY = PointClampIndex(
                ExactUvGeometry.Minimum(domain, false),
                texture.Height,
                domain.TexelScale);
            var maximumY = PointClampIndex(
                ExactUvGeometry.Maximum(domain, false),
                texture.Height,
                domain.TexelScale);
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                // An unbounded window cannot claim a bounded witness set;
                // unknown is the sound answer either way.
                return true;
            }

            return PointClampHasWitness(texture, domain, false, minimumX, maximumX, minimumY, maximumY);
        }

        /// <summary>
        /// The raw point clamp existence walk: one pass over the candidate
        /// window. Opaque texels never witness. An erased texel witnesses
        /// unless substitution skips it. The pre-filter stands down on
        /// clamp edges exactly as in the classified walk.
        /// </summary>
        private static bool PointClampHasWitness(
            AlphaTextureData texture,
            ExactUvDomain domain,
            bool substituteErased,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    if (alpha == AlphaTextureData.ErasedFlag && substituteErased)
                    {
                        continue;
                    }
                    if (canPreFilter)
                    {
                        // A clamp edge texel's exact support interval is a
                        // half line or the whole line at width or height
                        // one, so the finite pre-filter box may not decide
                        // for it while the triangle reaches past the box
                        // edge: below zero at the first index, above the
                        // size at the last.
                        var isBoundary = texture.Width == 1 || texture.Height == 1 ||
                                         (x == 0 && (v0x < 0 || v1x < 0 || v2x < 0)) ||
                                         (x == texture.Width - 1 && (v0x > texture.Width || v1x > texture.Width || v2x > texture.Width)) ||
                                         (y == 0 && (v0y < 0 || v1y < 0 || v2y < 0)) ||
                                         (y == texture.Height - 1 && (v0y > texture.Height || v1y > texture.Height || v2y > texture.Height));
                        // The translated extraction returns false past the
                        // proven bound. Then no cheap rejection happens, and
                        // the exact intersection test below decides alone.
                        if (!isBoundary &&
                            TryExtractTexelVerticesRelativeToCandidate(
                                domain, x, y,
                                out var c0x, out var c0y,
                                out var c1x, out var c1y,
                                out var c2x, out var c2y) &&
                            !ConservativeBilinearSupportOverlapsTriangle(
                                -0.5, 0.5, -0.5, 0.5,
                                c0x, c0y, c1x, c1y, c2x, c2y))
                        {
                            continue;
                        }
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        PointClampInterval(x, texture.Width, domain.TexelScale),
                        PointClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Decides whether the erased texels in the point clamp candidate
        /// window substitute for witnesses. The gate counts every candidate
        /// whose support interval intersects the domain. Opaque candidates
        /// count too. Erasure substitutes when the erased share of the
        /// consulted candidates stays at or under the policy. Equality
        /// moves. The collection walk runs first, so this full count runs
        /// only when the collected share exceeds the policy and the
        /// verdict could still flip.
        /// </summary>
        private static bool PointClampSubstitutesErasedTexels(
            AlphaTextureData texture,
            ExactUvDomain domain,
            int maxNoiseTexelPercent,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            if (!WindowHoldsErasedTexel(
                    texture,
                    minimumX,
                    maximumX,
                    minimumY,
                    maximumY))
            {
                return false;
            }

            var consulted = 0L;
            var erased = 0L;
            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    if (!ExactUvGeometry.Intersects(
                            domain,
                            PointClampInterval(x, texture.Width, domain.TexelScale),
                            PointClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (texture.GetAlpha(x, y) == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                    }
                }
            }

            return erased * 100 <= (long)maxNoiseTexelPercent * consulted;
        }

        private static TriangleAlphaOutcome ClassifyBilinearRepeat(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope,
            int maxNoiseTexelPercent)
        {
            var domain = ExactUvGeometry.NormalizeRepeat(
                ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope),
                texture.Width,
                texture.Height);
            if (!TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, true),
                    domain.TexelScale,
                    out var minCellX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, true),
                    domain.TexelScale,
                    out var maxCellX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, false),
                    domain.TexelScale,
                    out var minCellY) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, false),
                    domain.TexelScale,
                    out var maxCellY))
            {
                return TriangleAlphaOutcome.Unknown;
            }

            if (minCellX <= int.MinValue || maxCellX >= int.MaxValue ||
                minCellY <= int.MinValue || maxCellY >= int.MaxValue)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            var minimumX = minCellX - 1;
            var maximumX = maxCellX + 1;
            var minimumY = minCellY - 1;
            var maximumY = maxCellY + 1;

            if (minimumX <= int.MinValue || maximumX >= int.MaxValue ||
                minimumY <= int.MinValue || maximumY >= int.MaxValue)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            var spanX = (long)maximumX - minimumX + 1L;
            var spanY = (long)maximumY - minimumY + 1L;
            if (spanX <= 0L || spanY <= 0L ||
                spanX > MaxSupportRegions || spanY > MaxSupportRegions ||
                spanX * spanY > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            // The density gate decides whether erased texels act as
            // witnesses. A policy of zero keeps the plain walk: erased
            // texels stay witnesses, and no density evidence is collected.
            if (maxNoiseTexelPercent == 0)
            {
                return BilinearRepeatHasWitness(
                            texture,
                            domain,
                            false,
                            minimumX,
                            maximumX,
                            minimumY,
                            maximumY)
                    ? TriangleAlphaOutcome.MustRemainTransparent
                    : TriangleAlphaOutcome.ProvenOpaque;
            }

            // The gate is on: one walk collects the existence verdict and
            // the density evidence together. Opaque texels never witness,
            // so they stay unconsulted here. A consulted mid byte witnesses
            // under either substitution verdict, so the walk returns on it
            // at once. A consulted erased texel is only a candidate: it
            // witnesses when substitution fails, and the substitution share
            // is not known until the window has been fully walked.
            var consulted = 0L;
            var erased = 0L;
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    if (canPreFilter && !ConservativeBilinearSupportOverlapsTriangle(
                            unwrappedX - 0.5, unwrappedX + 1.5,
                            unwrappedY - 0.5, unwrappedY + 1.5,
                            v0x, v0y, v1x, v1y, v2x, v2y))
                    {
                        continue;
                    }
                    if (!ExactUvGeometry.Intersects(
                        domain,
                        BilinearRepeatInterval(unwrappedX, domain.TexelScale),
                        BilinearRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (alpha == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                        continue;
                    }
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
            }

            // Every consulted texel that reached the counters is an erased
            // candidate: the mid bytes returned above. When the collected
            // share meets the policy, the full count meets it too, because
            // the full count also consults opaque texels. No witness
            // remains, so the window proves opaque.
            if (erased * 100 <= (long)maxNoiseTexelPercent * consulted)
            {
                return TriangleAlphaOutcome.ProvenOpaque;
            }

            // The collected share exceeds the policy, so a candidate could
            // flip the verdict: the opaque consults may pull the share back
            // to or under the cap. The fallback decides with the full count
            // and the plain walk, exactly as the old two-pass did.
            var substituteErased = BilinearRepeatSubstitutesErasedTexels(
                texture,
                domain,
                maxNoiseTexelPercent,
                minimumX,
                maximumX,
                minimumY,
                maximumY);
            return BilinearRepeatHasWitness(
                        texture,
                        domain,
                        substituteErased,
                        minimumX,
                        maximumX,
                        minimumY,
                        maximumY)
                ? TriangleAlphaOutcome.MustRemainTransparent
                : TriangleAlphaOutcome.ProvenOpaque;
        }

        /// <summary>
        /// The mapped mirror of ClassifyBilinearRepeat: the same normalized
        /// domain, cell-index and overflow guards, one-texel footprint
        /// expansion, pre-filter, and repeat interval intersection, but the
        /// walk only asks whether one contributing texel stores a byte below
        /// the opaque maximum. Erased texels count as witnesses, and every
        /// unbounded give-up returns true: the witness bound that keeps red
        /// in [0, 1) and the decision honest through the map.
        /// </summary>
        internal static bool HasMappedWitnessBilinearRepeat(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.NormalizeRepeat(
                ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope),
                texture.Width,
                texture.Height);
            if (!TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, true),
                    domain.TexelScale,
                    out var minCellX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, true),
                    domain.TexelScale,
                    out var maxCellX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, false),
                    domain.TexelScale,
                    out var minCellY) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, false),
                    domain.TexelScale,
                    out var maxCellY))
            {
                return true;
            }

            if (minCellX <= int.MinValue || maxCellX >= int.MaxValue ||
                minCellY <= int.MinValue || maxCellY >= int.MaxValue)
            {
                return true;
            }

            var minimumX = minCellX - 1;
            var maximumX = maxCellX + 1;
            var minimumY = minCellY - 1;
            var maximumY = maxCellY + 1;

            if (minimumX <= int.MinValue || maximumX >= int.MaxValue ||
                minimumY <= int.MinValue || maximumY >= int.MaxValue)
            {
                return true;
            }

            var spanX = (long)maximumX - minimumX + 1L;
            var spanY = (long)maximumY - minimumY + 1L;
            if (spanX <= 0L || spanY <= 0L ||
                spanX > MaxSupportRegions || spanY > MaxSupportRegions ||
                spanX * spanY > MaxSupportRegions)
            {
                return true;
            }

            return BilinearRepeatHasWitness(texture, domain, false, minimumX, maximumX, minimumY, maximumY);
        }

        /// <summary>
        /// The raw bilinear repeat existence walk: one pass over the
        /// unwrapped candidate window. Opaque texels never witness. An
        /// erased texel witnesses unless substitution skips it.
        /// </summary>
        private static bool BilinearRepeatHasWitness(
            AlphaTextureData texture,
            ExactUvDomain domain,
            bool substituteErased,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    if (alpha == AlphaTextureData.ErasedFlag && substituteErased)
                    {
                        continue;
                    }
                    if (canPreFilter && !ConservativeBilinearSupportOverlapsTriangle(
                            unwrappedX - 0.5, unwrappedX + 1.5,
                            unwrappedY - 0.5, unwrappedY + 1.5,
                            v0x, v0y, v1x, v1y, v2x, v2y))
                    {
                        continue;
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        BilinearRepeatInterval(unwrappedX, domain.TexelScale),
                        BilinearRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Decides whether the erased texels in the bilinear repeat
        /// candidate window substitute for witnesses. The gate counts every
        /// candidate whose support interval intersects the domain. Opaque
        /// candidates count too. Erasure substitutes when the erased
        /// share of the consulted candidates stays at or under the
        /// policy. Equality moves. The collection walk runs first, so
        /// this full count runs only when the collected share exceeds
        /// the policy and the verdict could still flip.
        /// </summary>
        private static bool BilinearRepeatSubstitutesErasedTexels(
            AlphaTextureData texture,
            ExactUvDomain domain,
            int maxNoiseTexelPercent,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            if (!RepeatedWindowHoldsErasedTexel(
                    texture,
                    minimumX,
                    maximumX,
                    minimumY,
                    maximumY))
            {
                return false;
            }

            var consulted = 0L;
            var erased = 0L;
            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    if (!ExactUvGeometry.Intersects(
                            domain,
                            BilinearRepeatInterval(unwrappedX, domain.TexelScale),
                            BilinearRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (texture.GetAlpha(x, y) == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                    }
                }
            }

            return erased * 100 <= (long)maxNoiseTexelPercent * consulted;
        }

        private static ExactInterval BilinearRepeatInterval(
            int index,
            BigInteger texelScale)
        {
            var halfTexel = texelScale / 2;
            var center = new BigInteger(index) * texelScale;
            return new ExactInterval(
                true,
                new ExactRational(center - halfTexel),
                false,
                true,
                new ExactRational(center + 3 * halfTexel),
                false);
        }

        private static TriangleAlphaOutcome ClassifyBilinearClamp(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope,
            int maxNoiseTexelPercent)
        {
            var domain = ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope);
            var minimumX = Math.Max(0, PointClampIndex(
                ExactUvGeometry.Minimum(domain, true),
                texture.Width,
                domain.TexelScale) - 1);
            var maximumX = Math.Min(texture.Width - 1, PointClampIndex(
                ExactUvGeometry.Maximum(domain, true),
                texture.Width,
                domain.TexelScale) + 1);
            var minimumY = Math.Max(0, PointClampIndex(
                ExactUvGeometry.Minimum(domain, false),
                texture.Height,
                domain.TexelScale) - 1);
            var maximumY = Math.Min(texture.Height - 1, PointClampIndex(
                ExactUvGeometry.Maximum(domain, false),
                texture.Height,
                domain.TexelScale) + 1);
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            // The density gate decides whether erased texels act as
            // witnesses. A policy of zero keeps the plain walk: erased
            // texels stay witnesses, and no density evidence is collected.
            if (maxNoiseTexelPercent == 0)
            {
                return BilinearClampHasWitness(
                            texture,
                            domain,
                            false,
                            minimumX,
                            maximumX,
                            minimumY,
                            maximumY)
                    ? TriangleAlphaOutcome.MustRemainTransparent
                    : TriangleAlphaOutcome.ProvenOpaque;
            }

            // The gate is on: one walk collects the existence verdict and
            // the density evidence together. Opaque texels never witness,
            // so they stay unconsulted here. A consulted mid byte witnesses
            // under either substitution verdict, so the walk returns on it
            // at once. A consulted erased texel is only a candidate: it
            // witnesses when substitution fails, and the substitution share
            // is not known until the window has been fully walked.
            var consulted = 0L;
            var erased = 0L;
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    if (canPreFilter)
                    {
                        var isBoundary = texture.Width == 1 || texture.Height == 1 ||
                                         (x == 0 && (v0x < -0.5 || v1x < -0.5 || v2x < -0.5)) ||
                                         (x == texture.Width - 1 && (v0x > texture.Width - 0.5 || v1x > texture.Width - 0.5 || v2x > texture.Width - 0.5)) ||
                                         (y == 0 && (v0y < -0.5 || v1y < -0.5 || v2y < -0.5)) ||
                                         (y == texture.Height - 1 && (v0y > texture.Height - 0.5 || v1y > texture.Height - 0.5 || v2y > texture.Height - 0.5));
                        // The translated extraction returns false past the
                        // proven bound. Then no cheap rejection happens, and
                        // the exact intersection test below decides alone.
                        if (!isBoundary &&
                            TryExtractTexelVerticesRelativeToCandidate(
                                domain, x, y,
                                out var c0x, out var c0y,
                                out var c1x, out var c1y,
                                out var c2x, out var c2y) &&
                            !ConservativeBilinearSupportOverlapsTriangle(
                                -1, 1, -1, 1,
                                c0x, c0y, c1x, c1y, c2x, c2y))
                        {
                            continue;
                        }
                    }
                    if (!ExactUvGeometry.Intersects(
                        domain,
                        BilinearClampInterval(x, texture.Width, domain.TexelScale),
                        BilinearClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (alpha == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                        continue;
                    }
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
            }

            // Every consulted texel that reached the counters is an erased
            // candidate: the mid bytes returned above. When the collected
            // share meets the policy, the full count meets it too, because
            // the full count also consults opaque texels. No witness
            // remains, so the window proves opaque.
            if (erased * 100 <= (long)maxNoiseTexelPercent * consulted)
            {
                return TriangleAlphaOutcome.ProvenOpaque;
            }

            // The collected share exceeds the policy, so a candidate could
            // flip the verdict: the opaque consults may pull the share back
            // to or under the cap. The fallback decides with the full count
            // and the plain walk, exactly as the old two-pass did.
            var substituteErased = BilinearClampSubstitutesErasedTexels(
                texture,
                domain,
                maxNoiseTexelPercent,
                minimumX,
                maximumX,
                minimumY,
                maximumY);
            return BilinearClampHasWitness(
                        texture,
                        domain,
                        substituteErased,
                        minimumX,
                        maximumX,
                        minimumY,
                        maximumY)
                ? TriangleAlphaOutcome.MustRemainTransparent
                : TriangleAlphaOutcome.ProvenOpaque;
        }

        /// <summary>
        /// The mapped mirror of ClassifyBilinearClamp: the same clamped
        /// window, budget guard, boundary exception, pre-filter, and
        /// footprint interval intersection, but the walk only asks whether
        /// one contributing texel stores a byte below the opaque maximum.
        /// Erased texels count as witnesses, and an over-budget candidate
        /// window returns true: the witness bound that keeps red in [0, 1)
        /// and the decision honest through the map.
        /// </summary>
        private static bool HasMappedWitnessBilinearClamp(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope);
            var minimumX = Math.Max(0, PointClampIndex(
                ExactUvGeometry.Minimum(domain, true),
                texture.Width,
                domain.TexelScale) - 1);
            var maximumX = Math.Min(texture.Width - 1, PointClampIndex(
                ExactUvGeometry.Maximum(domain, true),
                texture.Width,
                domain.TexelScale) + 1);
            var minimumY = Math.Max(0, PointClampIndex(
                ExactUvGeometry.Minimum(domain, false),
                texture.Height,
                domain.TexelScale) - 1);
            var maximumY = Math.Min(texture.Height - 1, PointClampIndex(
                ExactUvGeometry.Maximum(domain, false),
                texture.Height,
                domain.TexelScale) + 1);
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                return true;
            }

            return BilinearClampHasWitness(texture, domain, false, minimumX, maximumX, minimumY, maximumY);
        }

        /// <summary>
        /// The raw bilinear clamp existence walk: one pass over the
        /// candidate window. Opaque texels never witness. An erased texel
        /// witnesses unless substitution skips it. The pre-filter stands
        /// down on clamp edges exactly as in the classified walk.
        /// </summary>
        private static bool BilinearClampHasWitness(
            AlphaTextureData texture,
            ExactUvDomain domain,
            bool substituteErased,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    if (alpha == AlphaTextureData.ErasedFlag && substituteErased)
                    {
                        continue;
                    }
                    if (canPreFilter)
                    {
                        var isBoundary = texture.Width == 1 || texture.Height == 1 ||
                                         (x == 0 && (v0x < -0.5 || v1x < -0.5 || v2x < -0.5)) ||
                                         (x == texture.Width - 1 && (v0x > texture.Width - 0.5 || v1x > texture.Width - 0.5 || v2x > texture.Width - 0.5)) ||
                                         (y == 0 && (v0y < -0.5 || v1y < -0.5 || v2y < -0.5)) ||
                                         (y == texture.Height - 1 && (v0y > texture.Height - 0.5 || v1y > texture.Height - 0.5 || v2y > texture.Height - 0.5));
                        // The translated extraction returns false past the
                        // proven bound. Then no cheap rejection happens, and
                        // the exact intersection test below decides alone.
                        if (!isBoundary &&
                            TryExtractTexelVerticesRelativeToCandidate(
                                domain, x, y,
                                out var c0x, out var c0y,
                                out var c1x, out var c1y,
                                out var c2x, out var c2y) &&
                            !ConservativeBilinearSupportOverlapsTriangle(
                                -1, 1, -1, 1,
                                c0x, c0y, c1x, c1y, c2x, c2y))
                        {
                            continue;
                        }
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        BilinearClampInterval(x, texture.Width, domain.TexelScale),
                        BilinearClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Decides whether the erased texels in the bilinear clamp
        /// candidate window substitute for witnesses. The gate counts every
        /// candidate whose support interval intersects the domain. Opaque
        /// candidates count too. Erasure substitutes when the erased
        /// share of the consulted candidates stays at or under the
        /// policy. Equality moves. The collection walk runs first, so
        /// this full count runs only when the collected share exceeds
        /// the policy and the verdict could still flip.
        /// </summary>
        private static bool BilinearClampSubstitutesErasedTexels(
            AlphaTextureData texture,
            ExactUvDomain domain,
            int maxNoiseTexelPercent,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            if (!WindowHoldsErasedTexel(
                    texture,
                    minimumX,
                    maximumX,
                    minimumY,
                    maximumY))
            {
                return false;
            }

            var consulted = 0L;
            var erased = 0L;
            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    if (!ExactUvGeometry.Intersects(
                            domain,
                            BilinearClampInterval(x, texture.Width, domain.TexelScale),
                            BilinearClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (texture.GetAlpha(x, y) == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                    }
                }
            }

            return erased * 100 <= (long)maxNoiseTexelPercent * consulted;
        }

        private static ExactInterval BilinearClampInterval(
            int index,
            int size,
            BigInteger texelScale)
        {
            if (size == 1)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    false,
                    default,
                    false);
            }

            var halfTexel = texelScale / 2;
            if (index == 0)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    true,
                    new ExactRational(3 * halfTexel),
                    false);
            }

            var center = new BigInteger(index) * texelScale;
            if (index == size - 1)
            {
                return new ExactInterval(
                    true,
                    new ExactRational(center - halfTexel),
                    false,
                    false,
                    default,
                    false);
            }
            return new ExactInterval(
                true,
                new ExactRational(center - halfTexel),
                false,
                true,
                new ExactRational(center + 3 * halfTexel),
                false);
        }

        /// <summary>
        /// Conservatively tests whether the bilinear reconstruction support box
        /// can intersect the triangle. Returns false only when the shapes are
        /// definitely separated. Returns true if they overlap or are within
        /// numerical tolerance.
        /// </summary>
        internal static bool ConservativeBilinearSupportOverlapsTriangle(
            double boxMinX, double boxMaxX,
            double boxMinY, double boxMaxY,
            double v0x, double v0y,
            double v1x, double v1y,
            double v2x, double v2y)
        {
            // Axis 1 and 2: AABB check.
            var triMinX = Math.Min(v0x, Math.Min(v1x, v2x));
            var triMaxX = Math.Max(v0x, Math.Max(v1x, v2x));
            if (boxMaxX < triMinX || boxMinX > triMaxX)
            {
                return false;
            }

            var triMinY = Math.Min(v0y, Math.Min(v1y, v2y));
            var triMaxY = Math.Max(v0y, Math.Max(v1y, v2y));
            if (boxMaxY < triMinY || boxMinY > triMaxY)
            {
                return false;
            }

            // Axis 3, 4, 5: Triangle edge normal half planes.
            if (EdgeSeparates(v0x, v0y, v1x, v1y, v2x, v2y, boxMinX, boxMaxX, boxMinY, boxMaxY))
            {
                return false;
            }
            if (EdgeSeparates(v1x, v1y, v2x, v2y, v0x, v0y, boxMinX, boxMaxX, boxMinY, boxMaxY))
            {
                return false;
            }
            if (EdgeSeparates(v2x, v2y, v0x, v0y, v1x, v1y, boxMinX, boxMaxX, boxMinY, boxMaxY))
            {
                return false;
            }

            return true;
        }

        private static bool EdgeSeparates(
            double aX, double aY,
            double bX, double bY,
            double cX, double cY,
            double boxMinX, double boxMaxX,
            double boxMinY, double boxMaxY)
        {
            var nx = -(bY - aY);
            var ny = bX - aX;

            // Sign of third triangle vertex.
            var cDot = nx * (cX - aX) + ny * (cY - aY);
            if (Math.Abs(cDot) < 1e-12)
            {
                return false;
            }

            // Find the box vertex that extends farthest toward the triangle interior.
            // If that extreme box vertex is outside, the whole box is outside.
            var extremeX = cDot > 0 ? (nx >= 0 ? boxMaxX : boxMinX) : (nx >= 0 ? boxMinX : boxMaxX);
            var extremeY = cDot > 0 ? (ny >= 0 ? boxMaxY : boxMinY) : (ny >= 0 ? boxMinY : boxMaxY);

            var extremeDot = nx * (extremeX - aX) + ny * (extremeY - aY);
            if (cDot > 0)
            {
                return extremeDot < -1e-9;
            }

            return extremeDot > 1e-9;
        }

        private static void ExtractTexelVertices(
            ExactUvDomain domain,
            out double v0x, out double v0y,
            out double v1x, out double v1y,
            out double v2x, out double v2y)
        {
            var scale = (double)domain.TexelScale;
            var pt0 = domain.Vertices[0];
            var pt1 = domain.Vertices[1];
            var pt2 = domain.Vertices[2];
            v0x = (double)pt0.X.Numerator / (double)pt0.X.Denominator / scale;
            v0y = (double)pt0.Y.Numerator / (double)pt0.Y.Denominator / scale;
            v1x = (double)pt1.X.Numerator / (double)pt1.X.Denominator / scale;
            v1y = (double)pt1.Y.Numerator / (double)pt1.Y.Denominator / scale;
            v2x = (double)pt2.X.Numerator / (double)pt2.X.Denominator / scale;
            v2y = (double)pt2.Y.Numerator / (double)pt2.Y.Denominator / scale;
        }

        /// <summary>
        /// Extracts the triangle vertices in texel units relative to one
        /// candidate support box center at (x + 0.5, y + 0.5). The
        /// subtraction runs on exact rationals, so the translation adds no
        /// error. The conversion to doubles stays inside the proven error
        /// stack only while every translated magnitude is at or below
        /// PreFilterSafeCoordinateBound. Returns false past that bound, and
        /// the caller then skips the pre-filter and falls through to the
        /// exact intersection test.
        /// </summary>
        private static bool TryExtractTexelVerticesRelativeToCandidate(
            ExactUvDomain domain,
            int x,
            int y,
            out double v0x, out double v0y,
            out double v1x, out double v1y,
            out double v2x, out double v2y)
        {
            var scale = domain.TexelScale;
            var centerX = new ExactRational(2 * (BigInteger)x + 1, 2);
            var centerY = new ExactRational(2 * (BigInteger)y + 1, 2);
            var first = TryTranslatedTexelVertex(
                domain.Vertices[0], scale, centerX, centerY, out v0x, out v0y);
            var second = TryTranslatedTexelVertex(
                domain.Vertices[1], scale, centerX, centerY, out v1x, out v1y);
            var third = TryTranslatedTexelVertex(
                domain.Vertices[2], scale, centerX, centerY, out v2x, out v2y);
            return first && second && third;
        }

        private static bool TryTranslatedTexelVertex(
            ExactUvPoint point,
            BigInteger scale,
            ExactRational centerX,
            ExactRational centerY,
            out double vx,
            out double vy)
        {
            var rx = ExactRational.Subtract(
                ExactRational.Divide(point.X, new ExactRational(scale)),
                centerX);
            var ry = ExactRational.Subtract(
                ExactRational.Divide(point.Y, new ExactRational(scale)),
                centerY);
            vx = (double)rx.Numerator / (double)rx.Denominator;
            vy = (double)ry.Numerator / (double)ry.Denominator;
            return Math.Abs(vx) <= PreFilterSafeCoordinateBound &&
                   Math.Abs(vy) <= PreFilterSafeCoordinateBound;
        }

        private static TriangleAlphaOutcome ClassifyPointRepeat(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope,
            int maxNoiseTexelPercent)
        {
            var domain = ExactUvGeometry.NormalizeRepeat(
                ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope),
                texture.Width,
                texture.Height);
            if (!TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, true),
                    domain.TexelScale,
                    out var minimumX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, true),
                    domain.TexelScale,
                    out var maximumX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, false),
                    domain.TexelScale,
                    out var minimumY) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, false),
                    domain.TexelScale,
                    out var maximumY))
            {
                return TriangleAlphaOutcome.Unknown;
            }

            if (minimumX <= int.MinValue || maximumX >= int.MaxValue ||
                minimumY <= int.MinValue || maximumY >= int.MaxValue)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            var spanX = (long)maximumX - minimumX + 1L;
            var spanY = (long)maximumY - minimumY + 1L;
            if (spanX <= 0L || spanY <= 0L ||
                spanX > MaxSupportRegions || spanY > MaxSupportRegions ||
                spanX * spanY > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            // The density gate decides whether erased texels act as
            // witnesses. A policy of zero keeps the plain walk: erased
            // texels stay witnesses, and no density evidence is collected.
            if (maxNoiseTexelPercent == 0)
            {
                return PointRepeatHasWitness(
                            texture,
                            domain,
                            false,
                            minimumX,
                            maximumX,
                            minimumY,
                            maximumY)
                    ? TriangleAlphaOutcome.MustRemainTransparent
                    : TriangleAlphaOutcome.ProvenOpaque;
            }

            // The gate is on: one walk collects the existence verdict and
            // the density evidence together. Opaque texels never witness,
            // so they stay unconsulted here. A consulted mid byte witnesses
            // under either substitution verdict, so the walk returns on it
            // at once. A consulted erased texel is only a candidate: it
            // witnesses when substitution fails, and the substitution share
            // is not known until the window has been fully walked.
            var consulted = 0L;
            var erased = 0L;
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    // Every repeat support interval is the bounded unit
                    // square at the unwrapped index, so the box never needs
                    // a clamp edge guard.
                    if (canPreFilter && !ConservativeBilinearSupportOverlapsTriangle(
                            unwrappedX, unwrappedX + 1,
                            unwrappedY, unwrappedY + 1,
                            v0x, v0y, v1x, v1y, v2x, v2y))
                    {
                        continue;
                    }
                    if (!ExactUvGeometry.Intersects(
                        domain,
                        PointRepeatInterval(unwrappedX, domain.TexelScale),
                        PointRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (alpha == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                        continue;
                    }
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
            }

            // Every consulted texel that reached the counters is an erased
            // candidate: the mid bytes returned above. When the collected
            // share meets the policy, the full count meets it too, because
            // the full count also consults opaque texels. No witness
            // remains, so the window proves opaque.
            if (erased * 100 <= (long)maxNoiseTexelPercent * consulted)
            {
                return TriangleAlphaOutcome.ProvenOpaque;
            }

            // The collected share exceeds the policy, so a candidate could
            // flip the verdict: the opaque consults may pull the share back
            // to or under the cap. The fallback decides with the full count
            // and the plain walk, exactly as the old two-pass did.
            var substituteErased = PointRepeatSubstitutesErasedTexels(
                texture,
                domain,
                maxNoiseTexelPercent,
                minimumX,
                maximumX,
                minimumY,
                maximumY);
            return PointRepeatHasWitness(
                        texture,
                        domain,
                        substituteErased,
                        minimumX,
                        maximumX,
                        minimumY,
                        maximumY)
                ? TriangleAlphaOutcome.MustRemainTransparent
                : TriangleAlphaOutcome.ProvenOpaque;
        }

        /// <summary>
        /// The mapped mirror of ClassifyPointRepeat: the same normalized
        /// domain, cell-index window, budget guard, and repeat interval
        /// intersection, but the walk only asks whether one contributing
        /// texel stores a byte below the opaque maximum. Erased texels count
        /// as witnesses, and every unbounded give-up returns true: the
        /// witness bound that keeps red in [0, 1) and the decision honest
        /// through the map.
        /// </summary>
        internal static bool HasMappedWitnessPointRepeat(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.NormalizeRepeat(
                ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope),
                texture.Width,
                texture.Height);
            if (!TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, true),
                    domain.TexelScale,
                    out var minimumX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, true),
                    domain.TexelScale,
                    out var maximumX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, false),
                    domain.TexelScale,
                    out var minimumY) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, false),
                    domain.TexelScale,
                    out var maximumY))
            {
                return true;
            }

            if (minimumX <= int.MinValue || maximumX >= int.MaxValue ||
                minimumY <= int.MinValue || maximumY >= int.MaxValue)
            {
                return true;
            }

            var spanX = (long)maximumX - minimumX + 1L;
            var spanY = (long)maximumY - minimumY + 1L;
            if (spanX <= 0L || spanY <= 0L ||
                spanX > MaxSupportRegions || spanY > MaxSupportRegions ||
                spanX * spanY > MaxSupportRegions)
            {
                return true;
            }

            return PointRepeatHasWitness(texture, domain, false, minimumX, maximumX, minimumY, maximumY);
        }

        /// <summary>
        /// The raw point repeat existence walk: one pass over the
        /// unwrapped candidate window. Opaque texels never witness. An
        /// erased texel witnesses unless substitution skips it.
        /// </summary>
        private static bool PointRepeatHasWitness(
            AlphaTextureData texture,
            ExactUvDomain domain,
            bool substituteErased,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    var alpha = texture.GetAlpha(x, y);
                    if (alpha == byte.MaxValue)
                    {
                        continue;
                    }
                    if (alpha == AlphaTextureData.ErasedFlag && substituteErased)
                    {
                        continue;
                    }
                    // Every repeat support interval is the bounded unit
                    // square at the unwrapped index, so the box never needs
                    // a clamp edge guard.
                    if (canPreFilter && !ConservativeBilinearSupportOverlapsTriangle(
                            unwrappedX, unwrappedX + 1,
                            unwrappedY, unwrappedY + 1,
                            v0x, v0y, v1x, v1y, v2x, v2y))
                    {
                        continue;
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        PointRepeatInterval(unwrappedX, domain.TexelScale),
                        PointRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Decides whether the erased texels in the point repeat candidate
        /// window substitute for witnesses. The gate counts every candidate
        /// whose support interval intersects the domain. Opaque candidates
        /// count too. Erasure substitutes when the erased share of the
        /// consulted candidates stays at or under the policy. Equality
        /// moves. The collection walk runs first, so this full count runs
        /// only when the collected share exceeds the policy and the
        /// verdict could still flip.
        /// </summary>
        private static bool PointRepeatSubstitutesErasedTexels(
            AlphaTextureData texture,
            ExactUvDomain domain,
            int maxNoiseTexelPercent,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            if (!RepeatedWindowHoldsErasedTexel(
                    texture,
                    minimumX,
                    maximumX,
                    minimumY,
                    maximumY))
            {
                return false;
            }

            var consulted = 0L;
            var erased = 0L;
            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    if (!ExactUvGeometry.Intersects(
                            domain,
                            PointRepeatInterval(unwrappedX, domain.TexelScale),
                            PointRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        continue;
                    }
                    consulted++;
                    if (texture.GetAlpha(x, y) == AlphaTextureData.ErasedFlag)
                    {
                        erased++;
                    }
                }
            }

            return erased * 100 <= (long)maxNoiseTexelPercent * consulted;
        }

        /// <summary>
        /// Reports whether any texel in the candidate window stores the
        /// erased flag. The density pre-pass walks the window once for this
        /// check. This avoids the exact intersection count when the window
        /// holds no erasure.
        /// </summary>
        private static bool WindowHoldsErasedTexel(
            AlphaTextureData texture,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    if (texture.GetAlpha(x, y) == AlphaTextureData.ErasedFlag)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Reports whether any texel under the repeated candidate window
        /// stores the erased flag. The window may span several texture
        /// tiles, so the walk folds every unwrapped index back onto the
        /// stored texels. The density pre-pass walks the window once for
        /// this check. This avoids the exact intersection count when the
        /// window holds no erasure.
        /// </summary>
        private static bool RepeatedWindowHoldsErasedTexel(
            AlphaTextureData texture,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    if (texture.GetAlpha(x, y) == AlphaTextureData.ErasedFlag)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetCellIndex(
            ExactRational coordinate,
            BigInteger texelScale,
            out int index)
        {
            var div = ExactUvGeometry.FloorDiv(
                coordinate.Numerator,
                coordinate.Denominator * texelScale);
            if (div < int.MinValue || div > int.MaxValue)
            {
                index = 0;
                return false;
            }
            index = (int)div;
            return true;
        }

        private static ExactInterval PointRepeatInterval(
            int index,
            BigInteger texelScale)
        {
            var center = new BigInteger(index) * texelScale;
            return new ExactInterval(
                true,
                new ExactRational(center),
                true,
                true,
                new ExactRational(center + texelScale),
                false);
        }

        private static int PointClampIndex(
            ExactRational coordinate,
            int size,
            BigInteger texelScale)
        {
            if (coordinate.CompareTo(new ExactRational(BigInteger.Zero)) <= 0)
            {
                return 0;
            }
            if (coordinate.CompareTo(new ExactRational(size * texelScale)) >= 0)
            {
                return size - 1;
            }
            return (int)(coordinate.Numerator /
                (coordinate.Denominator * texelScale));
        }

        private static ExactInterval PointClampInterval(
            int index,
            int size,
            BigInteger texelScale)
        {
            if (size == 1)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    false,
                    default,
                    false);
            }
            if (index == 0)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    true,
                    new ExactRational(texelScale),
                    false);
            }
            if (index == size - 1)
            {
                return new ExactInterval(
                    true,
                    new ExactRational(index * texelScale),
                    true,
                    false,
                    default,
                    false);
            }
            return new ExactInterval(
                true,
                new ExactRational(index * texelScale),
                true,
                true,
                new ExactRational((index + 1) * texelScale),
                false);
        }

        /// <summary>
        /// The density policy is a percentage. A value above 100 would let
        /// a fully erased consulted set prove a triangle without opaque
        /// evidence. Values outside the range are programming defects, so
        /// the classifier refuses them instead of repairing them.
        /// </summary>
        private static void ValidateDensityPolicy(int maxNoiseTexelPercent)
        {
            if (maxNoiseTexelPercent < 0 || maxNoiseTexelPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNoiseTexelPercent));
            }
        }


        private static void ValidateFinite(Vector2 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                throw new ArgumentException("UV0 values must be finite.", parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
