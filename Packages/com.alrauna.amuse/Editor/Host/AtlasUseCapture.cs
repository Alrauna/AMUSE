using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    // TRANSIENT HOST OBSERVATION ONLY: the outcome keeps the live
    // renderer so the pass can write back to the exact slot it came
    // from. The captured record beside it holds no live reference.
    internal sealed class AtlasUseOutcome
    {
        internal Renderer Renderer { get; }
        internal int SlotIndex { get; }

        /// <summary>
        /// The immutable record of the use, or null when the use
        /// refused.
        /// </summary>
        internal CapturedAtlasUse Use { get; }

        /// <summary>
        /// The refusal that stopped the capture, or
        /// <see cref="AtlasUseRefusal.None"/> when the use was admitted.
        /// </summary>
        internal AtlasUseRefusal Refusal { get; }

        /// <summary>The slot material, or null when the slot has none.</summary>
        internal Material Material { get; }

        /// <summary>
        /// The member texture when the capture read it, or null.
        /// </summary>
        internal Texture MemberTexture { get; }

        /// <summary>
        /// The sentence that names the exact bad value. It is empty when
        /// the use was admitted or when the cause has no detail.
        /// </summary>
        internal string Detail { get; }

        internal AtlasUseOutcome(
            Renderer renderer,
            int slotIndex,
            CapturedAtlasUse use,
            AtlasUseRefusal refusal,
            Material material,
            Texture memberTexture,
            string detail)
        {
            Renderer = renderer;
            SlotIndex = slotIndex;
            Use = use;
            Refusal = refusal;
            Material = material;
            MemberTexture = memberTexture;
            Detail = detail ?? string.Empty;
        }
    }

    /// <summary>
    /// Captures one use per renderer slot of the avatar, or the first
    /// refusal that stops that use. A use is one material slot of one
    /// renderer. The caller passes the committed animation graph it
    /// enumerated after the animator scope closed, so the capture sees
    /// every curve that a later pass committed.
    /// <para>
    /// The check order per slot is part of the contract. The first
    /// refusal wins, and the order runs from the renderer to the
    /// texture: renderer kind, mesh presence, topology, renderer shape
    /// animation, a slot that shares the last submesh, slot material
    /// presence, a material swap binding, the vendor lock, the shader
    /// row, the feature keywords, the required values, the assigned
    /// textures, the companion route values, property blocks
    /// renderer-wide first, animated route properties, the member
    /// texture, the texture transform, the UV0 channel, the unit
    /// square, and then the companion textures. An earlier check
    /// therefore never reads state a later refusal would have made
    /// unreachable.
    /// </para>
    /// <para>
    /// The animation breadth is deliberately wider than an exact match.
    /// A float binding whose property, without the <c>material.</c>
    /// prefix and without one component suffix, starts with a route or
    /// condition property name refuses the use. A prefix match refuses
    /// more than an exact match, because a prefix family such as
    /// <c>_MainTex</c> covers the transform and every suffixed variant
    /// of the property. That is safe. Milestone 2 refuses every animated
    /// route property, which also refuses more than an admission gate
    /// would. That is a coverage gap, not a false positive.
    /// </para>
    /// </summary>
    internal static class AtlasUseCapture
    {
        private const string MaterialPrefix = "material.";
        private const string MeshPropertyName = "m_Mesh";
        private const string SlotCountPropertyName =
            "m_Materials.Array.size";
        private const string MaterialNamePath = "m_Name";
        private const string CustomRenderQueuePath =
            "m_CustomRenderQueue";
        private const string InvalidKeywordsPath = "m_InvalidKeywords";
        private const string SavedPropertiesPath = "m_SavedProperties";
        private const string TexEnvsSectionName = "m_TexEnvs";
        private const string IntsSectionName = "m_Ints";
        private const string FloatsSectionName = "m_Floats";
        private const string ColorsSectionName = "m_Colors";

        /// <summary>
        /// The page formats of Milestone 2. Each is admitted in its
        /// sRGB and its linear form, because the page copies the exact
        /// source graphics format. A crunched variant refuses, because
        /// its bytes are not raw blocks.
        /// </summary>
        private static readonly HashSet<TextureFormat> SupportedPageFormats =
            new HashSet<TextureFormat>
            {
                TextureFormat.RGBA32,
                TextureFormat.RGB24,
                TextureFormat.DXT1,
                TextureFormat.DXT5,
                TextureFormat.BC4,
                TextureFormat.BC5,
                TextureFormat.BC7,
            };

        internal static IReadOnlyList<AtlasUseOutcome> Capture(
            GameObject avatarRoot,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            int protectedMipLevelCount)
        {
            if (avatarRoot == null)
            {
                throw new ArgumentNullException(nameof(avatarRoot));
            }

            // A null binding capability names the same fact as a refused
            // graph: the capture cannot observe animation, so every use
            // refuses. A null avatar root is a caller defect and throws.
            var graphReady =
                bindings != null &&
                graph != null &&
                graph.Refusal == AvatarAnimationRefusal.None;

            var observations =
                graphReady ? ObserveGraph(graph, bindings) : null;
            var declaredKeywords = new Dictionary<Shader, HashSet<string>>();

            var outcomes = new List<AtlasUseOutcome>();
            var renderers = avatarRoot
                .GetComponentsInChildren<Renderer>(true);
            var captureOrder = 0;
            for (var rendererIndex = 0;
                rendererIndex < renderers.Length;
                rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var slots = renderer.sharedMaterials;
                var mesh = MeshOf(renderer);
                var animation = AnimationFactsOf(
                    avatarRoot, renderer, observations);
                for (var slotIndex = 0;
                    slotIndex < slots.Length;
                    slotIndex++)
                {
                    CapturedAtlasUse use = null;
                    var refusal = AtlasUseRefusal.None;
                    Texture memberTexture = null;
                    var detail = string.Empty;
                    if (!graphReady)
                    {
                        refusal =
                            AtlasUseRefusal.AnimationGraphUnavailable;
                    }
                    else
                    {
                        CaptureSlot(
                            renderer,
                            rendererIndex,
                            slots,
                            slotIndex,
                            mesh,
                            animation,
                            protectedMipLevelCount,
                            declaredKeywords,
                            captureOrder,
                            out use,
                            out refusal,
                            out memberTexture,
                            out detail);
                    }

                    outcomes.Add(new AtlasUseOutcome(
                        renderer,
                        slotIndex,
                        use,
                        refusal,
                        slots[slotIndex],
                        memberTexture,
                        detail));
                    captureOrder++;
                }
            }

            return outcomes;
        }

        private static void CaptureSlot(
            Renderer renderer,
            int rendererIndex,
            Material[] slots,
            int slotIndex,
            Mesh mesh,
            RendererAnimationFacts animation,
            int protectedMipLevelCount,
            Dictionary<Shader, HashSet<string>> declaredKeywords,
            int captureOrder,
            out CapturedAtlasUse use,
            out AtlasUseRefusal refusal,
            out Texture memberTexture,
            out string detail)
        {
            use = null;
            refusal = AtlasUseRefusal.None;
            memberTexture = null;
            detail = string.Empty;

            if (!(renderer is MeshRenderer) &&
                !(renderer is SkinnedMeshRenderer))
            {
                refusal = AtlasUseRefusal.UnsupportedRenderer;
                detail = AtlasRefusalDetail.UnsupportedRenderer(renderer);
                return;
            }

            // A mesh without submeshes has no part any slot can draw,
            // and the clamped submesh read below would leave the valid
            // range, so both shapes refuse the same way.
            if (mesh == null || mesh.subMeshCount == 0)
            {
                refusal = AtlasUseRefusal.MissingMesh;
                detail = AtlasRefusalDetail.MissingMesh(mesh);
                return;
            }

            // A slot past the last submesh draws the last submesh again,
            // so its submesh read stays in range for the checks below.
            var submeshCount = mesh.subMeshCount;
            var submeshIndex = Math.Min(slotIndex, submeshCount - 1);
            var topology = mesh.GetTopology(submeshIndex);
            if (topology != MeshTopology.Triangles)
            {
                refusal = AtlasUseRefusal.NonTriangleTopology;
                detail = AtlasRefusalDetail.NonTriangleTopology(
                    mesh, submeshIndex, topology);
                return;
            }

            if (animation.ShapeAnimated)
            {
                refusal = AtlasUseRefusal.AnimatedRendererShape;
                detail = AtlasRefusalDetail.AnimatedProperty(
                    animation.ShapeProperty);
                return;
            }

            if (slots.Length > submeshCount &&
                slotIndex >= submeshCount - 1)
            {
                refusal = AtlasUseRefusal.SlotSharesSubmesh;
                detail = AtlasRefusalDetail.SlotSharesSubmesh(
                    slots.Length, mesh, submeshCount);
                return;
            }

            var material = slots[slotIndex];
            if (material == null)
            {
                refusal = AtlasUseRefusal.MissingMaterial;
                return;
            }

            if (animation.SwappedSlots.Contains(slotIndex))
            {
                refusal = AtlasUseRefusal.AnimatedMaterialSlot;
                return;
            }

            if (LockedMaterialIdentity.Classify(
                    SerializationOf(material)).IsLocked)
            {
                refusal = AtlasUseRefusal.LockedShader;
                detail = AtlasRefusalDetail.Shader(material.shader);
                return;
            }

            var rowRefusal =
                AttestRow(material, out var shaderRowName, out var table);
            if (rowRefusal != AtlasUseRefusal.None)
            {
                refusal = rowRefusal;
                detail = AtlasRefusalDetail.Shader(material.shader);
                return;
            }

            var disallowedKeywords = DisallowedKeywords(
                material, table, DeclaredKeywordsOf(
                    material.shader, declaredKeywords));
            if (disallowedKeywords.Count > 0)
            {
                refusal = AtlasUseRefusal.KeywordNotAllowed;
                detail = AtlasRefusalDetail.KeywordNotAllowed(
                    disallowedKeywords);
                return;
            }

            var unheldValue = table.UnheldRequiredValue(material);
            if (unheldValue != null)
            {
                refusal = AtlasUseRefusal.RequiredValueNotHeld;
                detail = unheldValue;
                return;
            }

            // A model exists only on a reviewed row. A toon standard
            // fallback tag swaps the shader at runtime, so every value
            // and every keyword counts and no stale name is skipped
            // there. The dead set feeds the route walk, so it is
            // computed before the walk.
            var liveValues = table.LiveValues;
            var deadProperties = NoDeadProperties;
            var toonStandardFallback = IsToonStandardFallback(material);
            var skipStaleSavedNames = false;
            if (liveValues != null)
            {
                skipStaleSavedNames = !toonStandardFallback;
                if (skipStaleSavedNames)
                {
                    deadProperties = liveValues.DeadProperties(
                        material,
                        shaderRowName,
                        SwitchIsDynamicOf(renderer, slotIndex, animation));
                }
            }

            var outlineRow = table.HasOutlinePass(shaderRowName);
            var resolution = ResolveRouteTextures(
                material, table, outlineRow, deadProperties);
            if (resolution.Unlisted.Count > 0)
            {
                refusal = AtlasUseRefusal.UnlistedTexture;
                detail = AtlasRefusalDetail.UnlistedTextures(
                    resolution.Unlisted);
                return;
            }

            foreach (var pending in resolution.PendingUv0)
            {
                var unheldRouteValue =
                    pending.Route.UnheldRequiredValue(material);
                if (unheldRouteValue != null)
                {
                    refusal = AtlasUseRefusal.RequiredValueNotHeld;
                    detail = unheldRouteValue;
                    return;
                }
            }

            var conditions = new List<string>(table.ConditionProperties);
            conditions.AddRange(resolution.ConditionProperties);

            var blockProperty = PropertyBlockRouteProperty(
                renderer,
                slotIndex,
                table.MemberProperty,
                conditions,
                out var rendererWide);
            if (blockProperty != null)
            {
                refusal = AtlasUseRefusal.PropertyBlockOnRouteProperty;
                detail = AtlasRefusalDetail.PropertyBlock(
                    blockProperty, rendererWide, slotIndex);
                return;
            }

            var animatedProperty = AnimatedRouteProperty(
                animation, table.MemberProperty, conditions);
            if (animatedProperty != null)
            {
                refusal = AtlasUseRefusal.AnimatedRouteProperty;
                detail = AtlasRefusalDetail.AnimatedProperty(
                    animatedProperty);
                return;
            }

            var texture = material.GetTexture(table.MemberProperty);
            memberTexture = texture;
            if (texture == null)
            {
                refusal = AtlasUseRefusal.MissingMainTexture;
                detail = AtlasRefusalDetail.MissingMainTexture(
                    table.MemberProperty);
                return;
            }

            if (!(texture is Texture2D texture2D) ||
                texture2D.dimension !=
                    UnityEngine.Rendering.TextureDimension.Tex2D)
            {
                refusal = AtlasUseRefusal.UnsupportedTextureDimension;
                detail = AtlasRefusalDetail.UnsupportedTextureDimension(
                    texture);
                return;
            }

            var sourceProblem = SourceAssetProblem(texture2D);
            if (sourceProblem != null)
            {
                refusal = AtlasUseRefusal.SourceNotAnAsset;
                detail = sourceProblem;
                return;
            }

            if (!SupportedPageFormats.Contains(texture2D.format))
            {
                refusal = AtlasUseRefusal.UnsupportedFormat;
                detail = AtlasRefusalDetail.UnsupportedFormat(texture2D);
                return;
            }

            if (!UnityTextureEvidence.TryGetAtlasSampler(
                    texture2D, out var sampler))
            {
                refusal = AtlasUseRefusal.UnsupportedWrap;
                detail = AtlasRefusalDetail.UnsupportedWrap(texture2D);
                return;
            }

            if (texture2D.mipmapCount < protectedMipLevelCount)
            {
                refusal = AtlasUseRefusal.SourceMipChainTooShort;
                detail = AtlasRefusalDetail.SourceMipChainTooShort(
                    texture2D, protectedMipLevelCount);
                return;
            }

            if (!SourceIsBlockAligned(texture2D, protectedMipLevelCount))
            {
                refusal = AtlasUseRefusal.SourceNotAligned;
                detail = AtlasRefusalDetail.SourceNotAligned(
                    texture2D,
                    protectedMipLevelCount,
                    BlockSizeOf(texture2D.format));
                return;
            }

            foreach (var transform in table.TransformProperties)
            {
                var value = material.GetVector(transform.Property);
                if (value.x != transform.Identity.x ||
                    value.y != transform.Identity.y ||
                    value.z != transform.Identity.z ||
                    value.w != transform.Identity.w)
                {
                    refusal = AtlasUseRefusal.NonIdentityTransform;
                    detail = AtlasRefusalDetail.NonIdentityTransform(
                        transform.Property, value, transform.Identity);
                    return;
                }
            }

            var channel = new List<Vector2>();
            mesh.GetUVs(0, channel);
            if (channel.Count == 0)
            {
                refusal = AtlasUseRefusal.MissingRouteChannel;
                detail = AtlasRefusalDetail.MissingRouteChannel(mesh);
                return;
            }

            if (mesh.GetIndexCount(submeshIndex) == 0)
            {
                refusal = AtlasUseRefusal.EmptySubmesh;
                detail = AtlasRefusalDetail.EmptySubmesh(mesh, submeshIndex);
                return;
            }

            if (!TryCaptureUvRect(
                    mesh,
                    submeshIndex,
                    channel,
                    out var uvRect,
                    out var offendingVertex))
            {
                refusal = AtlasUseRefusal.UvOutsideUnitSquare;
                detail = AtlasRefusalDetail.UvOutsideUnitSquare(
                    offendingVertex, channel[offendingVertex]);
                return;
            }

            var companions = new List<CapturedAtlasCompanion>();
            refusal = CaptureCompanions(
                material,
                outlineRow,
                resolution,
                texture2D,
                sampler,
                protectedMipLevelCount,
                companions,
                out detail);
            if (refusal != AtlasUseRefusal.None)
            {
                return;
            }

            use = new CapturedAtlasUse(
                rendererIndex,
                slotIndex,
                submeshIndex,
                shaderRowName,
                texture2D.GetInstanceID(),
                texture2D.graphicsFormat,
                texture2D.width,
                texture2D.height,
                texture2D.mipmapCount,
                sampler,
                uvRect,
                MaterialKeyOf(
                    material,
                    resolution.KeySkipped,
                    deadProperties,
                    liveValues,
                    skipStaleSavedNames,
                    toonStandardFallback),
                animation.AnimatedMaterialPropertyNames,
                captureOrder,
                companions,
                resolution.AliasProperties);
        }

        /// <summary>
        /// The inline trilinear Repeat sampler of a route without its
        /// own sampler state. It asks for no anisotropy and no bias.
        /// </summary>
        private static readonly AtlasSamplerFacts InlineTrilinearRepeat =
            new AtlasSamplerFacts(
                FilterMode.Trilinear,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                0,
                0f);

        /// <summary>
        /// The Poiyomi <c>sampler_linear_repeat</c> facts. The test
        /// suite reads the constants to pin them against the record.
        /// </summary>
        internal static readonly AtlasSamplerFacts InlineLinearRepeat =
            new AtlasSamplerFacts(
                FilterMode.Bilinear,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                0,
                0f);

        /// <summary>
        /// The Poiyomi <c>sampler_point_repeat</c> facts. The test
        /// suite reads the constants to pin them against the record.
        /// </summary>
        internal static readonly AtlasSamplerFacts InlinePointRepeat =
            new AtlasSamplerFacts(
                FilterMode.Point,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                0,
                0f);

        private const string OutlineTextureProperty = "_OutlineTex";

        /// <summary>
        /// Captures every pending UV0 companion in shader property
        /// order, or the first refusal. Per companion the order is the
        /// transforms, the texture dimension, the effective sampler, the
        /// member alias, the source asset, the format, the mip chain,
        /// the block alignment, and the size ratio. A companion that is
        /// the member texture under the member sampler facts becomes a
        /// page alias and needs no page. Then an outline row whose
        /// companions read <c>sampler_MainTex</c> needs an outline
        /// texture with the main sampler facts, because the outline
        /// pass reads <c>sampler_MainTex</c> as the outline sampler.
        /// </summary>
        private static AtlasUseRefusal CaptureCompanions(
            Material material,
            bool outlineRow,
            AtlasRouteResolution resolution,
            Texture2D main,
            AtlasSamplerFacts mainSampler,
            int protectedMipLevelCount,
            List<CapturedAtlasCompanion> companions,
            out string detail)
        {
            detail = string.Empty;
            var memberSamplerUsed = false;
            foreach (var pending in resolution.PendingUv0)
            {
                var route = pending.Route;
                var texture = pending.Texture;
                foreach (var transform in route.TransformProperties)
                {
                    var value = material.GetVector(transform.Property);
                    if (value.x != transform.Identity.x ||
                        value.y != transform.Identity.y ||
                        value.z != transform.Identity.z ||
                        value.w != transform.Identity.w)
                    {
                        detail = AtlasRefusalDetail.RouteTexture(
                            route.Property,
                            AtlasRefusalDetail.NonIdentityTransform(
                                transform.Property, value, transform.Identity));
                        return AtlasUseRefusal.NonIdentityTransform;
                    }
                }

                if (!(texture is Texture2D texture2D) ||
                    texture2D.dimension !=
                        UnityEngine.Rendering.TextureDimension.Tex2D)
                {
                    detail = AtlasRefusalDetail.RouteTexture(
                        route.Property,
                        AtlasRefusalDetail.UnsupportedTextureDimension(texture));
                    return AtlasUseRefusal.UnsupportedTextureDimension;
                }

                AtlasSamplerFacts facts;
                switch (route.Sampler)
                {
                    case AtlasCompanionSampler.Own:
                        if (!UnityTextureEvidence.TryGetAtlasSampler(
                                texture2D, out facts))
                        {
                            detail = AtlasRefusalDetail.RouteTexture(
                                route.Property,
                                AtlasRefusalDetail.UnsupportedWrap(texture2D));
                            return AtlasUseRefusal.UnsupportedWrap;
                        }

                        break;
                    case AtlasCompanionSampler.Member:
                        facts = mainSampler;
                        memberSamplerUsed = true;
                        break;
                    case AtlasCompanionSampler.InlineTrilinearRepeat:
                        facts = InlineTrilinearRepeat;
                        break;
                    case AtlasCompanionSampler.InlineLinearRepeat:
                        facts = InlineLinearRepeat;
                        break;
                    case AtlasCompanionSampler.InlinePointRepeat:
                        facts = InlinePointRepeat;
                        break;
                    case AtlasCompanionSampler.InlineClamp:
                        // A clamp companion refuses its UV0 use
                        // through its own required value before the
                        // sampler step runs. A pending use here
                        // means a route built its facts wrong.
                        throw new InvalidOperationException(
                            "The companion route '" + route.Property +
                            "' names the clamp sampler on a UV0 use." +
                            " The route must refuse that use before" +
                            " the sampler step.");
                    default:
                        throw new InvalidOperationException(
                            "The companion route '" + route.Property +
                            "' names the unknown sampler " + route.Sampler +
                            ".");
                }

                if (ReferenceEquals(texture2D, main) &&
                    facts.Equals(mainSampler))
                {
                    resolution.AliasProperties.Add(route.Property);
                    continue;
                }

                var sourceProblem = SourceAssetProblem(texture2D);
                if (sourceProblem != null)
                {
                    detail = AtlasRefusalDetail.RouteTexture(
                        route.Property, sourceProblem);
                    return AtlasUseRefusal.SourceNotAnAsset;
                }

                if (!SupportedPageFormats.Contains(texture2D.format))
                {
                    detail = AtlasRefusalDetail.RouteTexture(
                        route.Property,
                        AtlasRefusalDetail.UnsupportedFormat(texture2D));
                    return AtlasUseRefusal.UnsupportedFormat;
                }

                if (texture2D.mipmapCount < protectedMipLevelCount)
                {
                    detail = AtlasRefusalDetail.RouteTexture(
                        route.Property,
                        AtlasRefusalDetail.SourceMipChainTooShort(
                            texture2D, protectedMipLevelCount));
                    return AtlasUseRefusal.SourceMipChainTooShort;
                }

                if (!SourceIsBlockAligned(texture2D, protectedMipLevelCount))
                {
                    detail = AtlasRefusalDetail.RouteTexture(
                        route.Property,
                        AtlasRefusalDetail.SourceNotAligned(
                            texture2D,
                            protectedMipLevelCount,
                            BlockSizeOf(texture2D.format)));
                    return AtlasUseRefusal.SourceNotAligned;
                }

                var scaleShift = ScaleShiftOf(
                    main.width, main.height, texture2D.width, texture2D.height);
                if (scaleShift < 0)
                {
                    detail = AtlasRefusalDetail.RouteTexture(
                        route.Property,
                        AtlasRefusalDetail.CompanionSize(
                            texture2D, main.width, main.height));
                    return AtlasUseRefusal.CompanionSizeMismatch;
                }

                companions.Add(new CapturedAtlasCompanion(
                    route.Property,
                    texture2D.GetInstanceID(),
                    texture2D.graphicsFormat,
                    texture2D.width,
                    texture2D.height,
                    texture2D.mipmapCount,
                    facts,
                    scaleShift));
            }

            if (outlineRow && memberSamplerUsed)
            {
                var outline = material.GetTexture(OutlineTextureProperty);
                if (outline == null ||
                    !UnityTextureEvidence.TryGetAtlasSampler(
                        outline, out var outlineFacts) ||
                    !outlineFacts.Equals(mainSampler))
                {
                    detail = AtlasRefusalDetail.RouteTexture(
                        OutlineTextureProperty,
                        AtlasRefusalDetail.OutlineSampler(outline, main));
                    return AtlasUseRefusal.RouteSamplerMismatch;
                }
            }

            return AtlasUseRefusal.None;
        }

        /// <summary>
        /// The shift k with <c>mainWidth == width &lt;&lt; k</c> and
        /// <c>mainHeight == height &lt;&lt; k</c>, or -1 when no such k
        /// exists. A companion larger than the main texture has none.
        /// </summary>
        private static int ScaleShiftOf(
            int mainWidth,
            int mainHeight,
            int width,
            int height)
        {
            for (var shift = 0;
                (width << shift) <= mainWidth && shift < 31;
                shift++)
            {
                if ((width << shift) == mainWidth &&
                    (height << shift) == mainHeight)
                {
                    return shift;
                }
            }

            return -1;
        }


        // --- renderer and mesh facts -------------------------------------

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            if (renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                return filter == null ? null : filter.sharedMesh;
            }

            return null;
        }

        /// <summary>
        /// The UV bounding rectangle of one submesh on UV0. The unit
        /// square check is written as the negated closed range, so a
        /// NaN coordinate refuses instead of comparing unequal.
        /// </summary>
        private static bool TryCaptureUvRect(
            Mesh mesh,
            int submeshIndex,
            List<Vector2> channel,
            out AtlasUvRect uvRect,
            out int offendingVertex)
        {
            uvRect = default;
            offendingVertex = -1;
            var triangles = mesh.GetTriangles(submeshIndex);

            double minU = 1, minV = 1, maxU = 0, maxV = 0;
            foreach (var vertexIndex in triangles)
            {
                var coordinate = channel[vertexIndex];
                var u = coordinate.x;
                var v = coordinate.y;
                if (!(u >= 0 && u <= 1) || !(v >= 0 && v <= 1))
                {
                    offendingVertex = vertexIndex;
                    return false;
                }

                minU = Math.Min(minU, u);
                minV = Math.Min(minV, v);
                maxU = Math.Max(maxU, u);
                maxV = Math.Max(maxV, v);
            }

            uvRect = new AtlasUvRect(minU, minV, maxU, maxV);
            return true;
        }

        private static bool SourceIsBlockAligned(
            Texture2D texture,
            int protectedMipLevelCount)
        {
            var shift = protectedMipLevelCount - 1;
            var block = BlockSizeOf(texture.format);
            return (texture.width >> shift) % block == 0 &&
                   (texture.height >> shift) % block == 0;
        }

        /// <summary>
        /// Null when the texture is the main asset of a file a texture
        /// importer owns. Otherwise the sentence that names the
        /// problem. A native asset, a sub-asset, or a texture inside a
        /// model or scene file has a path, but no importer reproduces
        /// it, so the runtime acquisition cannot clone it. The rule is
        /// per use. Design section 4 condition 6.
        /// </summary>
        private static string SourceAssetProblem(Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return AtlasRefusalDetail.SourceOnlyInMemory(texture);
            }

            if (!(AssetImporter.GetAtPath(path) is TextureImporter))
            {
                return AtlasRefusalDetail.SourceWithoutImporter(path);
            }

            return ReferenceEquals(
                AssetDatabase.LoadMainAssetAtPath(path), texture)
                ? null
                : AtlasRefusalDetail.SourceNotMainAsset(texture, path);
        }

        private static int BlockSizeOf(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                case TextureFormat.BC4:
                case TextureFormat.BC5:
                case TextureFormat.BC7:
                    return 4;
                default:
                    return 1;
            }
        }

        // --- animation facts ----------------------------------------------

        /// <summary>
        /// The animation facts of one renderer path, read from one
        /// observation pass over every clip of the committed graph.
        /// </summary>
        private sealed class RendererAnimationFacts
        {
            internal bool ShapeAnimated => ShapeProperty != null;

            /// <summary>
            /// The first renderer shape property an animation drives,
            /// or null when no animation drives one.
            /// </summary>
            internal string ShapeProperty { get; private set; }

            internal void MarkShapeAnimated(string property)
            {
                if (ShapeProperty == null)
                {
                    ShapeProperty = property;
                }
            }

            internal HashSet<int> SwappedSlots { get; } =
                new HashSet<int>();

            /// <summary>
            /// The stripped property names of animated material float
            /// bindings, distinct, in observation order.
            /// </summary>
            internal List<string> AnimatedFloatProperties { get; } =
                new List<string>();

            /// <summary>
            /// The names the record carries. They are the stripped
            /// property names of the animated material float bindings,
            /// so two uses whose animations have different shapes never
            /// share a group key.
            /// </summary>
            internal List<string> AnimatedMaterialPropertyNames =>
                AnimatedFloatProperties;

            internal void AddAnimatedFloat(string stripped)
            {
                if (!AnimatedFloatProperties.Contains(stripped))
                {
                    AnimatedFloatProperties.Add(stripped);
                }
            }
        }

        private static List<LiveClipObservation> ObserveGraph(
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings)
        {
            var observations = new List<LiveClipObservation>();
            foreach (var layer in graph.Layers)
            {
                var isFromAdditiveLayer = layer.BlendingMode ==
                    AnimatorLayerBlendingMode.Additive;
                foreach (var clip in layer.Clips)
                {
                    observations.Add(LiveAnimationObservation.ObserveClip(
                        clip,
                        bindings.IsSpecialMotion(clip),
                        isFromAdditiveLayer));
                }
            }

            return observations;
        }

        private static RendererAnimationFacts AnimationFactsOf(
            GameObject avatarRoot,
            Renderer renderer,
            List<LiveClipObservation> observations)
        {
            var facts = new RendererAnimationFacts();
            if (observations == null)
            {
                return facts;
            }

            var path = AnimationUtility.CalculateTransformPath(
                renderer.transform, avatarRoot.transform);
            foreach (var observation in observations)
            {
                foreach (var floated in observation.Floats)
                {
                    if (!string.Equals(
                            floated.Path, path, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(
                            floated.PropertyName,
                            MeshPropertyName,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            floated.PropertyName,
                            SlotCountPropertyName,
                            StringComparison.Ordinal))
                    {
                        facts.MarkShapeAnimated(floated.PropertyName);
                        continue;
                    }

                    if (TryStripMaterialProperty(
                            floated.PropertyName, out var stripped))
                    {
                        facts.AddAnimatedFloat(stripped);
                    }
                }

                foreach (var referenced in observation.Objects)
                {
                    if (!string.Equals(
                            referenced.Path, path, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(
                            referenced.PropertyName,
                            MeshPropertyName,
                            StringComparison.Ordinal))
                    {
                        facts.MarkShapeAnimated(referenced.PropertyName);
                        continue;
                    }

                    if (LiveAnimationObservation
                        .TryParseMaterialSlotBinding(
                            referenced.PropertyName, out var swapped))
                    {
                        facts.SwappedSlots.Add(swapped);
                    }
                }
            }

            return facts;
        }

        /// <summary>
        /// The first animated property that names the member or a
        /// condition property, or null when no animation drives one.
        /// </summary>
        private static string AnimatedRouteProperty(
            RendererAnimationFacts animation,
            string memberProperty,
            IReadOnlyCollection<string> conditionProperties)
        {
            foreach (var stripped in animation.AnimatedFloatProperties)
            {
                if (stripped.StartsWith(
                        memberProperty, StringComparison.Ordinal))
                {
                    return stripped;
                }

                foreach (var condition in conditionProperties)
                {
                    if (stripped.StartsWith(
                            condition, StringComparison.Ordinal))
                    {
                        return stripped;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Strips the <c>material.</c> prefix and one component suffix
        /// such as <c>.x</c> or <c>.r</c> from an animated property
        /// name. Anything else stays whole.
        /// </summary>
        private static bool TryStripMaterialProperty(
            string propertyName,
            out string stripped)
        {
            stripped = null;
            if (propertyName == null ||
                !propertyName.StartsWith(
                    MaterialPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            stripped = propertyName.Substring(MaterialPrefix.Length);
            var lastDot = stripped.LastIndexOf('.');
            if (lastDot > 0 && lastDot == stripped.Length - 2)
            {
                var component = stripped[lastDot + 1];
                if (component == 'x' || component == 'y' ||
                    component == 'z' || component == 'w' ||
                    component == 'r' || component == 'g' ||
                    component == 'b' || component == 'a')
                {
                    stripped = stripped.Substring(0, lastDot);
                }
            }

            return true;
        }

        // --- material facts ------------------------------------------------

        private static LockedMaterialIdentity.Serialization
            SerializationOf(Material material)
        {
            var hasOptimizerEnabled = material.HasProperty(
                LockedMaterialIdentity.OptimizerEnabledPropertyName);
            return new LockedMaterialIdentity.Serialization(
                material.shader == null ? null : material.shader.name,
                hasOptimizerEnabled,
                hasOptimizerEnabled
                    ? material.GetFloat(
                        LockedMaterialIdentity.OptimizerEnabledPropertyName)
                    : 0f,
                TagOrNull(
                    material, LockedMaterialIdentity.OriginalShaderTagName),
                TagOrNull(
                    material,
                    LockedMaterialIdentity.OriginalShaderGuidTagName));
        }

        private static string TagOrNull(
            Material material,
            string tagName)
        {
            var value = material.GetTag(tagName, true, null);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// The shader row gate. It returns the route table of the one
        /// exactly attested row that the material shader matches. An
        /// attested lilToon Multi container has no route table in 0.2.0,
        /// so it refuses with its own name. Every other shader refuses
        /// as unattested. Version consent never extends a row here.
        /// </summary>
        private static AtlasUseRefusal AttestRow(
            Material material,
            out string shaderRowName,
            out AtlasRouteTable table)
        {
            table = null;
            if (TryAttestPoiyomi(
                    material, out shaderRowName, out var reviewed))
            {
                table = PoiyomiTableOf(reviewed);
                return AtlasUseRefusal.None;
            }

            var shader = material.shader;
            if (shader == null)
            {
                return AtlasUseRefusal.UnattestedShader;
            }

            if (LilToonMultiResolution.IsMultiContainerShaderName(shader.name))
            {
                return LilToonMultiAttests(material)
                    ? AtlasUseRefusal.NoRouteTable
                    : AtlasUseRefusal.UnattestedShader;
            }

            if (LilToonSourceAttestation.TryGatherSourceEvidenceForShaderName(
                    shader, LilToonIdentityEvidence(material),
                    out var lilToon, out _) &&
                LilToonSourceAttestation.TryVerifyExactRowIdentity(lilToon))
            {
                shaderRowName = shader.name;
                // The companion review read the 2.4.1 source only, so
                // an older admitted version keeps the plain route.
                table = string.Equals(
                        lilToon.PackageVersion,
                        LilToonSourceAttestation.PackageVersion,
                        StringComparison.Ordinal)
                    ? AtlasRouteTable.LilToonReviewedMainColor
                    : AtlasRouteTable.LilToonMainColor;
                return AtlasUseRefusal.None;
            }

            return AtlasUseRefusal.UnattestedShader;
        }

        /// <summary>
        /// The lilToon identity gather reads the shader name and the
        /// material shader-format stamp. No other material fact enters
        /// the identity conjunction.
        /// </summary>
        private static readonly MaterialEvidenceRequest
            LilToonIdentityRequest = new MaterialEvidenceRequest(
                true,
                false,
                Array.Empty<string>(),
                new[] { LilToonSourceAttestation.ShaderFormatVersionProperty },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<TexturePropertyEvidenceRequest>());

        private static CapturedMaterialEvidence LilToonIdentityEvidence(
            Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, LilToonIdentityRequest),
            })[0];
        }

        private static bool LilToonMultiAttests(Material material)
        {
            return LilToonSourceAttestation.TryResolveMultiContainer(
                       material.shader,
                       LilToonIdentityEvidence(material),
                       out var source,
                       out var profile,
                       out _) &&
                   LilToonSourceAttestation.TryVerifyMultiContainer(
                       source, profile, out _);
        }

        /// <summary>
        /// The route table of one attested Poiyomi row. The keyword
        /// review of 2026-11-23 read the two bundled 9.3 rows only,
        /// so a release row keeps the plain route until a review
        /// reads its source. The seam mirrors the lilToon version
        /// gate above.
        /// </summary>
        internal static AtlasRouteTable PoiyomiTableOf(bool atlasRouteReviewed)
        {
            return atlasRouteReviewed
                ? AtlasRouteTable.PoiyomiReviewedMainColor
                : AtlasRouteTable.PoiyomiMainColor;
        }

        /// <summary>
        /// The Poiyomi row gate: the name classifies, some row with this
        /// name holds this GUID, and the normalized source digest matches
        /// that same row. One row must hold all three values together.
        /// </summary>
        private static bool TryAttestPoiyomi(
            Material material,
            out string shaderRowName,
            out bool atlasRouteReviewed)
        {
            shaderRowName = null;
            atlasRouteReviewed = false;
            var shader = material.shader;
            if (shader == null ||
                !PoiyomiSourceAttestation.TryClassifyShaderName(
                    shader.name, out _))
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(assetPath) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    shader, out var guid, out long _))
            {
                return false;
            }

            var assetGuid = guid == null ? null : guid.ToLowerInvariant();
            if (string.IsNullOrEmpty(assetGuid) ||
                !RowHoldsNameAndGuid(shader.name, assetGuid))
            {
                return false;
            }

            try
            {
                if (!File.Exists(assetPath) ||
                    !PoiyomiSourceAttestation
                        .TryReadNormalizedSourceHashCached(
                            assetPath, out var hash) ||
                    !PoiyomiSourceAttestation.TryMatchAdmittedSource(
                        shader.name, assetGuid, hash, out var row))
                {
                    return false;
                }

                shaderRowName = row.ShaderName;
                atlasRouteReviewed = row.AtlasRouteReviewed;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool RowHoldsNameAndGuid(
            string shaderName,
            string assetGuid)
        {
            foreach (var row in PoiyomiSourceAttestation.AdmittedSources())
            {
                if (string.Equals(
                        row.ShaderName, shaderName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        row.AssetGuid, assetGuid, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Every enabled keyword the shader declares that the route
        /// does not allow, sorted ordinal. An empty list admits.
        /// </summary>
        private static List<string> DisallowedKeywords(
            Material material,
            AtlasRouteTable table,
            HashSet<string> declaredKeywords)
        {
            // Only a keyword the shader declares can change what the
            // shader samples. A keyword on the material that the shader
            // does not declare has no effect, so the capture ignores it.
            var disallowed = new List<string>();
            foreach (var keyword in material.enabledKeywords)
            {
                if (!declaredKeywords.Contains(keyword.name))
                {
                    continue;
                }

                var allowed = false;
                foreach (var entry in table.AllowedKeywords)
                {
                    if (string.Equals(
                            entry, keyword.name, StringComparison.Ordinal))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    disallowed.Add(keyword.name);
                }
            }

            disallowed.Sort(StringComparer.Ordinal);
            return disallowed;
        }

        private static HashSet<string> DeclaredKeywordsOf(
            Shader shader,
            Dictionary<Shader, HashSet<string>> memo)
        {
            if (memo.TryGetValue(shader, out var declared))
            {
                return declared;
            }

            var names = shader.keywordSpace.keywordNames;
            declared = new HashSet<string>(names, StringComparer.Ordinal);
            memo[shader] = declared;
            return declared;
        }

        /// <summary>
        /// The texture facts of one material under its route table.
        /// </summary>
        private sealed class AtlasRouteResolution
        {
            /// <summary>
            /// Every assigned texture the route does not list, in
            /// shader property order.
            /// </summary>
            internal readonly List<(string Property, Texture Texture)>
                Unlisted = new List<(string Property, Texture Texture)>();

            /// <summary>
            /// The texture properties the material key records as
            /// <c>member</c>: the member, the member copies, the dead
            /// textures, the inert routes that no fallback shader
            /// reads, and the assigned UV0 routes.
            /// </summary>
            internal readonly HashSet<string> KeySkipped =
                new HashSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// The per-material condition properties: the property, the
            /// transforms, and the conditions of each assigned route.
            /// </summary>
            internal readonly List<string> ConditionProperties =
                new List<string>();

            /// <summary>
            /// The assigned UV0 routes in shader property order.
            /// </summary>
            internal readonly List<(AtlasCompanionRoute Route, Texture Texture)>
                PendingUv0 =
                    new List<(AtlasCompanionRoute Route, Texture Texture)>();

            /// <summary>
            /// The properties the page material sets to the main page.
            /// </summary>
            internal readonly List<string> AliasProperties =
                new List<string>();
        }

        /// <summary>
        /// Sorts every texture property of the shader by its route.
        /// The enumeration walks the shader properties, so a texture
        /// that reachable code samples can never be missed. A dead
        /// texture that is not the member, a member copy, neutral, or
        /// a companion route leaves the key instead of refusing the
        /// slot, because no shader code samples it. Any other assigned
        /// texture without a route is unlisted. A companion route asks
        /// the material how it uses the texture. An inert texture
        /// leaves the key, unless a VRChat fallback shader can read
        /// the texture under its Standard name. A UV0 texture leaves
        /// the key and waits for the companion checks. A null texture
        /// on a UV0 route stays in the key, so a material without the
        /// texture never joins a group whose page material binds one.
        /// </summary>
        private static AtlasRouteResolution ResolveRouteTextures(
            Material material,
            AtlasRouteTable table,
            bool outlineRow,
            ISet<string> deadProperties)
        {
            var resolution = new AtlasRouteResolution();
            var shader = material.shader;
            var count = ShaderUtil.GetPropertyCount(shader);
            for (var index = 0; index < count; index++)
            {
                if (ShaderUtil.GetPropertyType(shader, index) !=
                    UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    continue;
                }

                var name = ShaderUtil.GetPropertyName(shader, index);
                var texture = material.GetTexture(name);
                if (string.Equals(
                        name, table.MemberProperty, StringComparison.Ordinal))
                {
                    resolution.KeySkipped.Add(name);
                    continue;
                }

                if (Lists(table.MemberCopyProperties, name))
                {
                    resolution.KeySkipped.Add(name);
                    resolution.AliasProperties.Add(name);
                    continue;
                }

                if (Lists(table.NeutralProperties, name))
                {
                    continue;
                }

                var route = RouteOf(table, name);
                if (route == null)
                {
                    if (texture != null)
                    {
                        if (deadProperties.Contains(name))
                        {
                            resolution.KeySkipped.Add(name);
                        }
                        else
                        {
                            resolution.Unlisted.Add((name, texture));
                        }
                    }

                    continue;
                }

                if (texture != null)
                {
                    resolution.ConditionProperties.Add(route.Property);
                    foreach (var transform in route.TransformProperties)
                    {
                        resolution.ConditionProperties.Add(transform.Property);
                    }

                    resolution.ConditionProperties.AddRange(
                        route.ConditionProperties);
                }

                switch (route.UseOf(material, outlineRow))
                {
                    case AtlasRouteUse.Inert:
                        // A fallback shader reads the texture under
                        // its Standard name even behind an off switch,
                        // so the texture stays keyed and still splits
                        // groups.
                        if (!FallbackPropertyNames.Contains(name))
                        {
                            resolution.KeySkipped.Add(name);
                        }

                        break;
                    case AtlasRouteUse.OtherCoordinates:
                        break;
                    case AtlasRouteUse.Uv0:
                        if (texture != null)
                        {
                            resolution.KeySkipped.Add(name);
                            resolution.PendingUv0.Add((route, texture));
                        }

                        break;
                    default:
                        throw new InvalidOperationException(
                            "The companion route '" + name +
                            "' returned an unknown use.");
                }
            }

            return resolution;
        }

        private static bool Lists(
            IReadOnlyCollection<string> entries,
            string name)
        {
            foreach (var entry in entries)
            {
                if (string.Equals(entry, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static AtlasCompanionRoute RouteOf(
            AtlasRouteTable table,
            string name)
        {
            foreach (var route in table.CompanionRoutes)
            {
                if (string.Equals(
                        route.Property, name, StringComparison.Ordinal))
                {
                    return route;
                }
            }

            return null;
        }

        /// <summary>
        /// The property block gate. The renderer-wide block is read
        /// first, because its entries apply to every slot. The per-slot
        /// block is read second, and Unity keeps the two stores apart,
        /// so a slot block refuses only its own slot. The result is the
        /// first route property a block sets, or null.
        /// </summary>
        private static string PropertyBlockRouteProperty(
            Renderer renderer,
            int slotIndex,
            string memberProperty,
            IReadOnlyCollection<string> conditionProperties,
            out bool rendererWide)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var property = BlockRouteProperty(
                block, memberProperty, conditionProperties);
            if (property != null)
            {
                rendererWide = true;
                return property;
            }

            rendererWide = false;
            renderer.GetPropertyBlock(block, slotIndex);
            return BlockRouteProperty(
                block, memberProperty, conditionProperties);
        }

        private static string BlockRouteProperty(
            MaterialPropertyBlock block,
            string memberProperty,
            IReadOnlyCollection<string> conditionProperties)
        {
            if (BlockNamesRoute(block, memberProperty))
            {
                return memberProperty;
            }

            foreach (var condition in conditionProperties)
            {
                if (BlockNamesRoute(block, condition))
                {
                    return condition;
                }
            }

            return null;
        }

        private static bool BlockNamesRoute(
            MaterialPropertyBlock block,
            string propertyName)
        {
            return block.HasTexture(propertyName) ||
                   block.HasVector(propertyName) ||
                   block.HasFloat(propertyName);
        }

        /// <summary>
        /// Whether a family switch may hold any value at runtime. The
        /// rule reads the same facts as the refusal gates: an animated
        /// material binding with the shared prefix rule, and the two
        /// renderer property blocks with the shared block lookup.
        /// </summary>
        private static Func<string, bool> SwitchIsDynamicOf(
            Renderer renderer,
            int slotIndex,
            RendererAnimationFacts animation)
        {
            // The blocks are read on the first query only, so a row
            // whose families are all on never pays for them.
            MaterialPropertyBlock rendererWide = null;
            MaterialPropertyBlock slotBlock = null;
            var factsRead = false;
            return switchProperty =>
            {
                if (!factsRead)
                {
                    rendererWide = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(rendererWide);
                    slotBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(slotBlock, slotIndex);
                    factsRead = true;
                }

                foreach (var animated in animation
                    .AnimatedMaterialPropertyNames)
                {
                    if (animated.StartsWith(
                            switchProperty,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return BlockNamesRoute(rendererWide, switchProperty) ||
                       BlockNamesRoute(slotBlock, switchProperty);
            };
        }
        /// <summary>
        /// Whether the material carries a VRCFallback tag that names
        /// the toon standard shader. VRChat swaps the shader then, so
        /// the fallback shader reads values that the row never reads.
        /// </summary>
        private static bool IsToonStandardFallback(Material material)
        {
            return material
                .GetTag("VRCFallback", false, string.Empty)
                .StartsWith(
                    "toonstandard",
                    StringComparison.OrdinalIgnoreCase);
        }

        // --- the material key ----------------------------------------------

        /// <summary>
        /// The material key of one use. The key is a deterministic
        /// string from one <see cref="SerializedObject"/> walk of
        /// the material, one token for the effective render queue,
        /// and the saved properties keyed by name. The walk skips
        /// the asset name, because two uses of the same state under
        /// different names must share a page. It skips the custom
        /// render queue and the invalid keywords, because the queue
        /// token holds the effective queue and a keyword the shader
        /// does not declare cannot change rendering. A toon standard
        /// fallback tag makes VRChat copy same-named keywords, so the
        /// walk keeps the invalid keywords there. It records each
        /// skipped texture reference as <c>member</c>: the member and
        /// every UV0 companion move into pages that serve every
        /// source of the group, and an inert or member copy texture
        /// is never sampled. When a live value model covers the row,
        /// the walk also drops each dead family value, each stale
        /// saved name that no shader declares, and each fallback
        /// texture entry with no texture. Every float enters as its
        /// exact bits and every other object reference as its
        /// instance id, so near-equal states stay distinct. The key
        /// is build-local. No consumer stores it.
        /// </summary>
        private static string MaterialKeyOf(
            Material material,
            ISet<string> skippedTextureProperties,
            ISet<string> deadProperties,
            AtlasLiveValueModel liveValues,
            bool skipStaleSavedNames,
            bool keepInvalidKeywords)
        {
            return new MaterialKeyWalker(
                    skippedTextureProperties,
                    deadProperties,
                    liveValues,
                    skipStaleSavedNames,
                    keepInvalidKeywords)
                .Walk(material);
        }

        /// <summary>
        /// The dead set for every row without a reviewed model, so
        /// every saved entry still counts in the key.
        /// </summary>
        private static readonly ISet<string> NoDeadProperties =
            new HashSet<string>();

        /// <summary>
        /// The fallback names as a set, because the walk queries one
        /// saved name at a time.
        /// </summary>
        private static readonly HashSet<string> FallbackPropertyNames =
            new HashSet<string>(Semantics.VRChatFallbackProperties.Names);

        private sealed class MaterialKeyWalker
        {
            private readonly ISet<string> skippedTextureProperties;
            private readonly ISet<string> deadProperties;
            private readonly AtlasLiveValueModel liveValues;
            private readonly bool skipStaleSavedNames;
            private readonly bool keepInvalidKeywords;
            private readonly HashSet<string> shaderPropertyNames =
                new HashSet<string>();
            private readonly StringBuilder builder =
                new StringBuilder(4096);

            internal MaterialKeyWalker(
                ISet<string> skippedTextureProperties,
                ISet<string> deadProperties,
                AtlasLiveValueModel liveValues,
                bool skipStaleSavedNames,
                bool keepInvalidKeywords)
            {
                this.skippedTextureProperties = skippedTextureProperties;
                this.deadProperties = deadProperties;
                this.liveValues = liveValues;
                this.skipStaleSavedNames = skipStaleSavedNames;
                this.keepInvalidKeywords = keepInvalidKeywords;
            }

            internal string Walk(Material material)
            {
                // The names feed the stale rule only, so a walk
                // without that rule skips the shader query.
                if (skipStaleSavedNames)
                {
                    CollectShaderPropertyNames(material);
                }

                using var serialized = new SerializedObject(material);
                var property = serialized.GetIterator();
                var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = Visit(property);
                }

                AppendEffectiveQueue(material);
                AppendSavedProperties(serialized);
                return builder.ToString();
            }

            /// <summary>
            /// The property names of the row shader. A saved name
            /// that the row declares is live, whatever any model
            /// says about other rows.
            /// </summary>
            private void CollectShaderPropertyNames(Material material)
            {
                var shader = material.shader;
                if (shader == null)
                {
                    return;
                }

                var count = shader.GetPropertyCount();
                for (var index = 0; index < count; index++)
                {
                    shaderPropertyNames.Add(shader.GetPropertyName(index));
                }
            }

            /// <summary>
            /// Whether one path names a subtree that the generic walk
            /// must skip. The name is identity. The custom queue
            /// enters as the effective queue. An undeclared keyword
            /// cannot change rendering, unless a toon standard
            /// fallback tag makes VRChat copy it to the fallback
            /// shader. The saved properties enter by name after the
            /// walk.
            /// </summary>
            private bool IsSkippedSubtree(string path)
            {
                return string.Equals(
                           path, MaterialNamePath,
                           StringComparison.Ordinal) ||
                       string.Equals(
                           path, CustomRenderQueuePath,
                           StringComparison.Ordinal) ||
                       (!keepInvalidKeywords &&
                           string.Equals(
                               path, InvalidKeywordsPath,
                               StringComparison.Ordinal)) ||
                       string.Equals(
                           path, SavedPropertiesPath,
                           StringComparison.Ordinal);
            }

            private bool Visit(SerializedProperty property)
            {
                if (IsSkippedSubtree(property.propertyPath))
                {
                    return false;
                }

                switch (property.propertyType)
                {
                    case SerializedPropertyType.Generic:
                        builder.Append(property.propertyPath).Append("={");
                        return true;
                    case SerializedPropertyType.ObjectReference:
                        builder.Append(property.propertyPath).Append('=');
                        var reference = property.objectReferenceValue;
                        builder.Append(
                            reference == null
                                ? "null"
                                : "o" + reference.GetInstanceID());
                        builder.Append(';');
                        return false;
                    case SerializedPropertyType.String:
                        builder.Append(property.propertyPath)
                            .Append("=s")
                            .Append(property.stringValue ?? string.Empty)
                            .Append(';');
                        return false;
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Character:
                        AppendPath(property);
                        builder.Append('i')
                            .Append(property.intValue)
                            .Append(';');
                        return false;
                    case SerializedPropertyType.Boolean:
                        AppendPath(property);
                        builder.Append('b')
                            .Append(property.boolValue ? 1 : 0)
                            .Append(';');
                        return false;
                    case SerializedPropertyType.Float:
                        AppendPath(property);
                        builder.Append("f")
                            .Append(
                                BitConverter.SingleToInt32Bits(
                                    property.floatValue)
                                    .ToString("X8"))
                            .Append(';');
                        return false;
                    case SerializedPropertyType.Vector2:
                        AppendPath(property);
                        var vector2 = property.vector2Value;
                        builder.Append("v2")
                            .Append(Bits(vector2.x))
                            .Append(',')
                            .Append(Bits(vector2.y))
                            .Append(';');
                        return false;
                    case SerializedPropertyType.Vector3:
                        AppendPath(property);
                        var vector3 = property.vector3Value;
                        builder.Append("v3")
                            .Append(Bits(vector3.x))
                            .Append(',')
                            .Append(Bits(vector3.y))
                            .Append(',')
                            .Append(Bits(vector3.z))
                            .Append(';');
                        return false;
                    case SerializedPropertyType.Vector4:
                        AppendPath(property);
                        var vector4 = property.vector4Value;
                        builder.Append("v4")
                            .Append(Bits(vector4.x))
                            .Append(',')
                            .Append(Bits(vector4.y))
                            .Append(',')
                            .Append(Bits(vector4.z))
                            .Append(',')
                            .Append(Bits(vector4.w))
                            .Append(';');
                        return false;
                    case SerializedPropertyType.Color:
                        AppendPath(property);
                        var color = property.colorValue;
                        builder.Append("c")
                            .Append(Bits(color.r))
                            .Append(',')
                            .Append(Bits(color.g))
                            .Append(',')
                            .Append(Bits(color.b))
                            .Append(',')
                            .Append(Bits(color.a))
                            .Append(';');
                        return false;
                    case SerializedPropertyType.ArraySize:
                        AppendPath(property);
                        builder.Append('n')
                            .Append(property.intValue)
                            .Append(';');
                        return false;
                    default:
                        throw new InvalidOperationException(
                            "The material key walk met the unsupported " +
                            "serialized property type " +
                            property.propertyType + " at " +
                            property.propertyPath + ".");
                }
            }

            private void AppendEffectiveQueue(Material material)
            {
                // The shader reference is already in the key, so the
                // effective queue alone separates two page materials.
                // An explicit queue that equals the shader queue is
                // the same render state as no explicit queue.
                builder.Append("queue=i")
                    .Append(material.renderQueue)
                    .Append(';');
            }

            /// <summary>
            /// Appends the saved properties by name in four fixed
            /// sections. The saved array order follows the property
            /// order of earlier shaders, so keying by name keeps one
            /// key for one render state when a stale entry shifts
            /// the arrays. A dead property adds no token. A section
            /// that Unity does not write adds nothing. A direct
            /// child with an unknown name refuses the walk, because
            /// an unknown shape may hold a live value.
            /// </summary>
            private void AppendSavedProperties(
                SerializedObject serialized)
            {
                var saved = serialized.FindProperty(SavedPropertiesPath);
                if (saved == null)
                {
                    return;
                }

                SerializedProperty texEnvs = null;
                SerializedProperty ints = null;
                SerializedProperty floats = null;
                SerializedProperty colors = null;
                var child = saved.Copy();
                var end = child.GetEndProperty();
                var enterChildren = true;
                while (child.Next(enterChildren) &&
                       !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;
                    if (string.Equals(
                            child.name, TexEnvsSectionName,
                            StringComparison.Ordinal))
                    {
                        texEnvs = child.Copy();
                    }
                    else if (string.Equals(
                                 child.name, IntsSectionName,
                                 StringComparison.Ordinal))
                    {
                        ints = child.Copy();
                    }
                    else if (string.Equals(
                                 child.name, FloatsSectionName,
                                 StringComparison.Ordinal))
                    {
                        floats = child.Copy();
                    }
                    else if (string.Equals(
                                 child.name, ColorsSectionName,
                                 StringComparison.Ordinal))
                    {
                        colors = child.Copy();
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "The material key walk met the unknown " +
                            "saved properties section '" + child.name +
                            "' at " + child.propertyPath + ".");
                    }
                }

                AppendNamedSection(texEnvs, "t:", TexEnvValueOf, true);
                AppendNamedSection(ints, "i:", IntValueOf, false);
                AppendNamedSection(floats, "f:", FloatValueOf, false);
                AppendNamedSection(colors, "c:", ColorValueOf, false);
            }

            /// <summary>
            /// Appends one saved section by name. The walk drops a
            /// dead family value, a stale saved name, and a fallback
            /// texture entry that holds no texture. A stale name is
            /// one that the row does not declare, that no reviewed
            /// uniform list declares, and that no VRChat fallback
            /// shader reads. Such a value cannot reach a shader.
            /// </summary>
            private void AppendNamedSection(
                SerializedProperty array,
                string prefix,
                Func<string, SerializedProperty, string> formatValue,
                bool texEnvSection)
            {
                if (array == null)
                {
                    return;
                }

                var tokens = new List<(string Name, string Value)>();
                for (var index = 0; index < array.arraySize; index++)
                {
                    var element = array.GetArrayElementAtIndex(index);
                    var name = SavedNameOf(element);
                    if (deadProperties.Contains(name))
                    {
                        continue;
                    }

                    if (skipStaleSavedNames && IsStaleSavedName(name))
                    {
                        continue;
                    }

                    if (texEnvSection && HoldsNoFallbackTexture(name, element))
                    {
                        continue;
                    }

                    tokens.Add((name, formatValue(name, element)));
                }

                tokens.Sort((left, right) =>
                {
                    var byName = string.Compare(
                        left.Name, right.Name, StringComparison.Ordinal);
                    return byName != 0
                        ? byName
                        : string.Compare(
                            left.Value, right.Value,
                            StringComparison.Ordinal);
                });
                foreach (var token in tokens)
                {
                    builder.Append(prefix)
                        .Append(token.Name)
                        .Append('=')
                        .Append(token.Value)
                        .Append(';');
                }
            }

            private bool IsStaleSavedName(string name)
            {
                return !shaderPropertyNames.Contains(name) &&
                       !liveValues.Declares(name) &&
                       !FallbackPropertyNames.Contains(name);
            }

            /// <summary>
            /// Whether the entry names a fallback texture property
            /// and holds no texture. A missing entry and a null entry
            /// are the same material state, so the pair adds no
            /// token. The rule needs a model, because only a
            /// reviewed row knows the fallback names.
            /// </summary>
            private bool HoldsNoFallbackTexture(
                string name, SerializedProperty element)
            {
                if (liveValues == null ||
                    !FallbackPropertyNames.Contains(name))
                {
                    return false;
                }

                var texture = element
                    .FindPropertyRelative("second")
                    .FindPropertyRelative("m_Texture")
                    .objectReferenceValue;
                return texture == null;
            }

            private string TexEnvValueOf(
                string name, SerializedProperty element)
            {
                var second = element.FindPropertyRelative("second");
                var texture = second
                    .FindPropertyRelative("m_Texture")
                    .objectReferenceValue;
                var scale = second
                    .FindPropertyRelative("m_Scale").vector2Value;
                var offset = second
                    .FindPropertyRelative("m_Offset").vector2Value;
                return TextureIdentityOf(name, texture) +
                       "," + Bits(scale.x) + "," + Bits(scale.y) +
                       "," + Bits(offset.x) + "," + Bits(offset.y);
            }

            /// <summary>
            /// The texture identity of one saved pair. A skipped
            /// texture is <c>member</c>, assigned or not, because the
            /// page serves every source of the group.
            /// </summary>
            private string TextureIdentityOf(
                string name, UnityEngine.Object texture)
            {
                if (skippedTextureProperties.Contains(name))
                {
                    return "member";
                }

                return texture == null
                    ? "null"
                    : "o" + texture.GetInstanceID();
            }

            private static string IntValueOf(
                string name, SerializedProperty element)
            {
                return element.FindPropertyRelative("second")
                    .intValue.ToString();
            }

            private static string FloatValueOf(
                string name, SerializedProperty element)
            {
                return Bits(element.FindPropertyRelative("second")
                    .floatValue);
            }

            private static string ColorValueOf(
                string name, SerializedProperty element)
            {
                var color = element.FindPropertyRelative("second")
                    .colorValue;
                return Bits(color.r) + "," + Bits(color.g) + "," +
                       Bits(color.b) + "," + Bits(color.a);
            }

            /// <summary>
            /// The saved property name of one array element. Unity
            /// stores the pair name either as a plain string under
            /// <c>first</c> or as <c>first.name</c> in the struct
            /// shape. An element without a readable name refuses
            /// the walk, because an unknown shape may hold a live
            /// value.
            /// </summary>
            private static string SavedNameOf(
                SerializedProperty element)
            {
                var first = element.FindPropertyRelative("first");
                string name = null;
                if (first != null &&
                    first.propertyType ==
                        SerializedPropertyType.String)
                {
                    name = first.stringValue;
                }
                else if (first != null)
                {
                    var inner = first.FindPropertyRelative("name");
                    if (inner != null &&
                        inner.propertyType ==
                            SerializedPropertyType.String)
                    {
                        name = inner.stringValue;
                    }
                }

                if (name == null)
                {
                    throw new InvalidOperationException(
                        "The material key walk met a saved property " +
                        "element without a readable name at " +
                        element.propertyPath + ".");
                }

                return name;
            }

            private void AppendPath(SerializedProperty property)
            {
                builder.Append(property.propertyPath).Append('=');
            }

            private static string Bits(float value)
            {
                return BitConverter.SingleToInt32Bits(value)
                    .ToString("X8");
            }
        }
    }
}
