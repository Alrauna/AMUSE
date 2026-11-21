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

        internal AtlasUseOutcome(
            Renderer renderer,
            int slotIndex,
            CapturedAtlasUse use,
            AtlasUseRefusal refusal)
        {
            Renderer = renderer;
            SlotIndex = slotIndex;
            Use = use;
            Refusal = refusal;
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
    /// textures, property blocks renderer-wide first, animated route
    /// properties, the member texture, the texture transform, the UV0
    /// channel, and the unit square. An earlier check therefore never
    /// reads state a later refusal would have made unreachable.
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
        private const string TexEnvStringNameSuffix = ".first";
        private const string TexEnvStructNameSuffix = ".first.name";
        private const string TexEnvTextureSuffix = ".second.m_Texture";
        private const string MaterialNamePath = "m_Name";

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
                            out refusal);
                    }

                    outcomes.Add(new AtlasUseOutcome(
                        renderer, slotIndex, use, refusal));
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
            out AtlasUseRefusal refusal)
        {
            use = null;
            refusal = AtlasUseRefusal.None;

            if (!(renderer is MeshRenderer) &&
                !(renderer is SkinnedMeshRenderer))
            {
                refusal = AtlasUseRefusal.UnsupportedRenderer;
                return;
            }

            // A mesh without submeshes has no part any slot can draw,
            // and the clamped submesh read below would leave the valid
            // range, so both shapes refuse the same way.
            if (mesh == null || mesh.subMeshCount == 0)
            {
                refusal = AtlasUseRefusal.MissingMesh;
                return;
            }

            // A slot past the last submesh draws the last submesh again,
            // so its submesh read stays in range for the checks below.
            var submeshCount = mesh.subMeshCount;
            var submeshIndex = Math.Min(slotIndex, submeshCount - 1);
            if (mesh.GetTopology(submeshIndex) != MeshTopology.Triangles)
            {
                refusal = AtlasUseRefusal.NonTriangleTopology;
                return;
            }

            if (animation.ShapeAnimated)
            {
                refusal = AtlasUseRefusal.AnimatedRendererShape;
                return;
            }

            if (slots.Length > submeshCount &&
                slotIndex >= submeshCount - 1)
            {
                refusal = AtlasUseRefusal.SlotSharesSubmesh;
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
                return;
            }

            var rowRefusal =
                AttestRow(material, out var shaderRowName, out var table);
            if (rowRefusal != AtlasUseRefusal.None)
            {
                refusal = rowRefusal;
                return;
            }

            if (!KeywordsAllowed(
                    material, table, DeclaredKeywordsOf(
                        material.shader, declaredKeywords)))
            {
                refusal = AtlasUseRefusal.KeywordNotAllowed;
                return;
            }

            if (!table.HoldsRequiredValues(material))
            {
                refusal = AtlasUseRefusal.RequiredValueNotHeld;
                return;
            }

            if (!AllTexturesListed(material, table))
            {
                refusal = AtlasUseRefusal.UnlistedTexture;
                return;
            }

            if (PropertyBlockTouchesRoute(renderer, slotIndex, table))
            {
                refusal = AtlasUseRefusal.PropertyBlockOnRouteProperty;
                return;
            }

            if (RoutePropertyAnimated(animation, table))
            {
                refusal = AtlasUseRefusal.AnimatedRouteProperty;
                return;
            }

            var texture = material.GetTexture(table.MemberProperty);
            if (texture == null)
            {
                refusal = AtlasUseRefusal.MissingMainTexture;
                return;
            }

            if (!(texture is Texture2D texture2D) ||
                texture2D.dimension !=
                    UnityEngine.Rendering.TextureDimension.Tex2D)
            {
                refusal = AtlasUseRefusal.UnsupportedTextureDimension;
                return;
            }

            if (!SourceIsImportedTextureAsset(texture2D))
            {
                refusal = AtlasUseRefusal.SourceNotAnAsset;
                return;
            }

            if (!SupportedPageFormats.Contains(texture2D.format))
            {
                refusal = AtlasUseRefusal.UnsupportedFormat;
                return;
            }

            if (!UnityTextureEvidence.TryGetAtlasSampler(
                    texture2D, out var sampler))
            {
                refusal = AtlasUseRefusal.UnsupportedWrap;
                return;
            }

            if (texture2D.mipmapCount < protectedMipLevelCount)
            {
                refusal = AtlasUseRefusal.SourceMipChainTooShort;
                return;
            }

            if (!SourceIsBlockAligned(texture2D, protectedMipLevelCount))
            {
                refusal = AtlasUseRefusal.SourceNotAligned;
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
                    return;
                }
            }

            var channel = new List<Vector2>();
            mesh.GetUVs(0, channel);
            if (channel.Count == 0)
            {
                refusal = AtlasUseRefusal.MissingRouteChannel;
                return;
            }

            if (mesh.GetIndexCount(submeshIndex) == 0)
            {
                refusal = AtlasUseRefusal.EmptySubmesh;
                return;
            }

            if (!TryCaptureUvRect(
                    mesh, submeshIndex, channel, out var uvRect))
            {
                refusal = AtlasUseRefusal.UvOutsideUnitSquare;
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
                MaterialKeyOf(material, table),
                animation.AnimatedMaterialPropertyNames,
                captureOrder);
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
            out AtlasUvRect uvRect)
        {
            uvRect = default;
            var triangles = mesh.GetTriangles(submeshIndex);

            double minU = 1, minV = 1, maxU = 0, maxV = 0;
            foreach (var vertexIndex in triangles)
            {
                var coordinate = channel[vertexIndex];
                var u = coordinate.x;
                var v = coordinate.y;
                if (!(u >= 0 && u <= 1) || !(v >= 0 && v <= 1))
                {
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
        /// True when the texture is the main asset of a file a texture
        /// importer owns. A native asset, a sub-asset, or a texture
        /// inside a model or scene file has a path, but no importer
        /// reproduces it, so the runtime acquisition cannot clone it.
        /// The rule is per use. Design section 4 condition 6.
        /// </summary>
        private static bool SourceIsImportedTextureAsset(
            Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (!(AssetImporter.GetAtPath(path) is TextureImporter))
            {
                return false;
            }

            return ReferenceEquals(
                AssetDatabase.LoadMainAssetAtPath(path), texture);
        }

        private static int BlockSizeOf(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
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
            internal bool ShapeAnimated { get; set; }

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
                        facts.ShapeAnimated = true;
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
                        facts.ShapeAnimated = true;
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

        private static bool RoutePropertyAnimated(
            RendererAnimationFacts animation,
            AtlasRouteTable table)
        {
            foreach (var stripped in animation.AnimatedFloatProperties)
            {
                if (stripped.StartsWith(
                        table.MemberProperty, StringComparison.Ordinal))
                {
                    return true;
                }

                foreach (var condition in table.ConditionProperties)
                {
                    if (stripped.StartsWith(
                            condition, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
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
            if (TryAttestPoiyomi(material, out shaderRowName))
            {
                table = AtlasRouteTable.PoiyomiMainColor;
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
                table = AtlasRouteTable.LilToonMainColor;
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
        /// The Poiyomi row gate: the name classifies, some row with this
        /// name holds this GUID, and the normalized source digest matches
        /// that same row. One row must hold all three values together.
        /// </summary>
        private static bool TryAttestPoiyomi(
            Material material,
            out string shaderRowName)
        {
            shaderRowName = null;
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

        private static bool KeywordsAllowed(
            Material material,
            AtlasRouteTable table,
            HashSet<string> declaredKeywords)
        {
            // Only a keyword the shader declares can change what the
            // shader samples. A keyword on the material that the shader
            // does not declare has no effect, so the capture ignores it.
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
                    return false;
                }
            }

            return true;
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
        /// Every assigned texture must be the member or a neutral
        /// texture. The enumeration walks the shader properties, so a
        /// texture that reachable code samples can never be missed.
        /// </summary>
        private static bool AllTexturesListed(
            Material material,
            AtlasRouteTable table)
        {
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
                if (texture == null)
                {
                    continue;
                }

                if (string.Equals(
                        name, table.MemberProperty, StringComparison.Ordinal))
                {
                    continue;
                }

                var neutral = false;
                foreach (var entry in table.NeutralProperties)
                {
                    if (string.Equals(
                            entry, name, StringComparison.Ordinal))
                    {
                        neutral = true;
                        break;
                    }
                }

                if (!neutral)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The property block gate. The renderer-wide block is read
        /// first, because its entries apply to every slot. The per-slot
        /// block is read second, and Unity keeps the two stores apart,
        /// so a slot block refuses only its own slot.
        /// </summary>
        private static bool PropertyBlockTouchesRoute(
            Renderer renderer,
            int slotIndex,
            AtlasRouteTable table)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (BlockTouchesRoute(block, table))
            {
                return true;
            }

            renderer.GetPropertyBlock(block, slotIndex);
            return BlockTouchesRoute(block, table);
        }

        private static bool BlockTouchesRoute(
            MaterialPropertyBlock block,
            AtlasRouteTable table)
        {
            if (BlockNamesRoute(block, table.MemberProperty))
            {
                return true;
            }

            foreach (var condition in table.ConditionProperties)
            {
                if (BlockNamesRoute(block, condition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BlockNamesRoute(
            MaterialPropertyBlock block,
            string propertyName)
        {
            return block.HasTexture(propertyName) ||
                   block.HasVector(propertyName) ||
                   block.HasFloat(propertyName);
        }

        // --- the material key ----------------------------------------------

        /// <summary>
        /// The material key of one use. The key is a deterministic
        /// string from one <see cref="SerializedObject"/> walk of the
        /// material. The walk skips the asset name, because two uses of
        /// the same state under different names must share a page. It
        /// skips the member texture reference, because the member moves
        /// into the page and the page serves every source of the group.
        /// Every float enters as its exact bits and every object
        /// reference as its instance id, so near-equal states stay
        /// distinct. The key is build-local. No consumer stores it.
        /// </summary>
        private static string MaterialKeyOf(
            Material material,
            AtlasRouteTable table)
        {
            return new MaterialKeyWalker(table.MemberProperty)
                .Walk(material);
        }

        private sealed class MaterialKeyWalker
        {
            private readonly string memberProperty;
            private readonly StringBuilder builder =
                new StringBuilder(4096);
            private readonly Dictionary<string, string> texEnvNames =
                new Dictionary<string, string>(StringComparer.Ordinal);

            internal MaterialKeyWalker(string memberProperty)
            {
                this.memberProperty = memberProperty;
            }

            internal string Walk(Material material)
            {
                using var serialized = new SerializedObject(material);
                var property = serialized.GetIterator();
                var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = Visit(property);
                }

                return builder.ToString();
            }

            private bool Visit(SerializedProperty property)
            {
                if (string.Equals(
                        property.propertyPath,
                        MaterialNamePath,
                        StringComparison.Ordinal))
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
                        if (IsMemberTextureReference(property.propertyPath))
                        {
                            builder.Append("member");
                        }
                        else
                        {
                            var reference = property.objectReferenceValue;
                            builder.Append(
                                reference == null
                                    ? "null"
                                    : "o" + reference.GetInstanceID());
                        }

                        builder.Append(';');
                        return false;
                    case SerializedPropertyType.String:
                        RecordTexEnvName(
                            property.propertyPath, property.stringValue);
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

            private void AppendPath(SerializedProperty property)
            {
                builder.Append(property.propertyPath).Append('=');
            }

            /// <summary>
            /// Records the property name of one texture environment
            /// element. The serialized pair stores the name either as
            /// a plain string under <c>.first</c> or, in the struct
            /// shape, as <c>.first.name</c>. Both shapes are recorded
            /// under the element path prefix, so the texture reference
            /// of that element can be matched later.
            /// </summary>
            private void RecordTexEnvName(string path, string name)
            {
                if (path.EndsWith(
                        TexEnvStringNameSuffix, StringComparison.Ordinal))
                {
                    texEnvNames[PathPrefix(
                        path, TexEnvStringNameSuffix.Length)] =
                        name ?? string.Empty;
                }
                else if (path.EndsWith(
                             TexEnvStructNameSuffix,
                             StringComparison.Ordinal))
                {
                    texEnvNames[PathPrefix(
                        path, TexEnvStructNameSuffix.Length)] =
                        name ?? string.Empty;
                }
            }

            /// <summary>
            /// Whether one object reference property is the texture
            /// reference of the member texture environment. The
            /// element prefix of the reference path names the element,
            /// and the recorded name must equal the member property.
            /// </summary>
            private bool IsMemberTextureReference(string path)
            {
                if (!path.EndsWith(
                        TexEnvTextureSuffix, StringComparison.Ordinal))
                {
                    return false;
                }

                return texEnvNames.TryGetValue(
                    PathPrefix(path, TexEnvTextureSuffix.Length),
                    out var elementName) &&
                    string.Equals(
                        elementName,
                        memberProperty,
                        StringComparison.Ordinal);
            }

            private static string PathPrefix(
                string path,
                int suffixLength)
            {
                return path.Substring(0, path.Length - suffixLength);
            }

            private static string Bits(float value)
            {
                return BitConverter.SingleToInt32Bits(value)
                    .ToString("X8");
            }
        }
    }
}
