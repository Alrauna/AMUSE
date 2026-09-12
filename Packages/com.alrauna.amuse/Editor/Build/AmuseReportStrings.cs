using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The English string table for every AMUSE report. Short sentences,
    /// active voice, one idea per sentence: the reader sees what stopped,
    /// why it stopped, and what happens next. Every key a report can ask
    /// for must exist here; the completeness test enumerates the refusal
    /// vocabularies so a new cause without strings fails the build tests.
    /// </summary>
    internal static class AmuseReportStrings
    {
        private const string Prefix = "amuse.";

        private static readonly Dictionary<string, string> Strings =
            new(StringComparer.Ordinal)
        {
            // --- Renderer refusals ---
            ["amuse.renderer.UnsupportedRendererType"] =
                "AMUSE skipped this renderer.",
            ["amuse.renderer.UnsupportedRendererType:description"] =
                "AMUSE only reads Mesh Renderer and Skinned Mesh Renderer " +
                "components. This renderer keeps its original materials.",
            ["amuse.renderer.UnsupportedRendererType:hint"] =
                "Nothing on this renderer is wrong. AMUSE just does not " +
                "work with this renderer type yet.",

            ["amuse.renderer.MaterialPropertyOverridesPresent"] =
                "This renderer has material property overrides.",
            ["amuse.renderer.MaterialPropertyOverridesPresent:description"] =
                "A material property block changes this renderer's " +
                "materials while the game runs. AMUSE cannot prove what " +
                "the renderer shows, so it changed nothing.",
            ["amuse.renderer.MaterialPropertyOverridesPresent:hint"] =
                "If you do not need the property overrides, remove them " +
                "and AMUSE can analyze this renderer.",

            ["amuse.renderer.MaterialDependencyClosureFailed"] =
                "AMUSE could not read this renderer's animations.",
            ["amuse.renderer.MaterialDependencyClosureFailed:description"] =
                "An animation on this avatar swaps a material on this " +
                "renderer, and AMUSE cannot prove what that animation " +
                "shows. The renderer keeps its original materials.",
            ["amuse.renderer.MaterialDependencyClosureFailed:hint"] =
                "Check the animations that touch this renderer. Every " +
                "material they use must be one AMUSE supports.",

            ["amuse.renderer.UnrecognizedAnimatedMaterialBinding"] =
                "This renderer has an animated property AMUSE does not know.",
            ["amuse.renderer.UnrecognizedAnimatedMaterialBinding:description"] =
                "An animation changes a material property that AMUSE does " +
                "not recognize, so AMUSE cannot prove what the renderer " +
                "shows. The renderer keeps its original materials.",
            ["amuse.renderer.UnrecognizedAnimatedMaterialBinding:hint"] =
                "Keep the animations limited to properties AMUSE knows, " +
                "or accept that this renderer stays unchanged.",

            ["amuse.renderer.MissingMesh"] =
                "This renderer has no mesh.",
            ["amuse.renderer.MissingMesh:description"] =
                "A Skinned Mesh Renderer needs a mesh. AMUSE changed " +
                "nothing.",
            ["amuse.renderer.MissingMesh:hint"] =
                "Assign a mesh to the renderer, or delete the renderer.",

            ["amuse.renderer.UnprovenMaterialSlotMapping"] =
                "This renderer's material slots do not match its mesh.",
            ["amuse.renderer.UnprovenMaterialSlotMapping:description"] =
                "The mesh has a different number of parts than the " +
                "renderer has material slots. AMUSE only works when the " +
                "numbers match.",
            ["amuse.renderer.UnprovenMaterialSlotMapping:hint"] =
                "Make the number of material slots equal the number of " +
                "mesh parts.",

            ["amuse.renderer.UnsupportedTopology"] =
                "This mesh uses a topology AMUSE does not support.",
            ["amuse.renderer.UnsupportedTopology:description"] =
                "AMUSE only proves triangles on triangle meshes. This " +
                "renderer keeps its original materials.",
            ["amuse.renderer.UnsupportedTopology:hint"] =
                "Convert the mesh to triangles.",

            ["amuse.renderer.MalformedMeshData"] =
                "This mesh has data AMUSE cannot read.",
            ["amuse.renderer.MalformedMeshData:description"] =
                "The mesh is missing vertex data that AMUSE needs. The " +
                "renderer keeps its original materials.",
            ["amuse.renderer.MalformedMeshData:hint"] =
                "Re-export or repair the mesh.",

            ["amuse.renderer.AnimatedMeshReplacement"] =
                "An animation replaces this renderer's mesh.",
            ["amuse.renderer.AnimatedMeshReplacement:description"] =
                "AMUSE cannot follow mesh swaps, so it changed nothing on " +
                "this renderer.",
            ["amuse.renderer.AnimatedMeshReplacement:hint"] =
                "Remove the mesh-swap animation if you want AMUSE to run " +
                "on this renderer.",

            ["amuse.renderer.AnimatedMaterialSlotCount"] =
                "An animation changes this renderer's material slot count.",
            ["amuse.renderer.AnimatedMaterialSlotCount:description"] =
                "AMUSE cannot follow animations that add or remove " +
                "material slots. The renderer keeps its original materials.",
            ["amuse.renderer.AnimatedMaterialSlotCount:hint"] =
                "Remove the slot-count animation if you want AMUSE to run " +
                "on this renderer.",

            ["amuse.renderer.AdditiveLayerWithProofRelevantMaterialProperty"] =
                "This avatar has an additive animation layer that touches " +
                "this renderer.",
            ["amuse.renderer.AdditiveLayerWithProofRelevantMaterialProperty:description"] =
                "Additive layers blend on top of other layers, so AMUSE " +
                "cannot prove what material property values the renderer " +
                "ends up with. The renderer keeps its original materials.",
            ["amuse.renderer.UnsupportedAnimationCurveForm"] =
                "This renderer has an animation AMUSE cannot interpret.",
            ["amuse.renderer.UnsupportedAnimationCurveForm:description"] =
                "An animation curve on this avatar uses a form AMUSE " +
                "cannot read. The renderer keeps its original materials.",
            ["amuse.renderer.UnsupportedAnimationCurveForm:hint"] =
                "Rebuild the offending animation curve.",
            ["amuse.consent.Subject"] =
                "Unverified version: {0}",
            ["amuse.renderer.AnimatedMaterialPropertyNotSingleton"] =
                "An animation gives this renderer different property " +
                "values in different places.",
            ["amuse.renderer.AnimatedMaterialPropertyNotSingleton:description"] =
                "The same material property would need two different " +
                "values at the same time, so AMUSE cannot prove a single " +
                "answer. The renderer keeps its original materials.",
            ["amuse.renderer.AnimatedMaterialPropertyNotSingleton:hint"] =
                "Remove the conflicting animation curves.",

            ["amuse.renderer.AdmittedMaterialSemanticsUnknown"] =
                "AMUSE could not prove any triangle on this renderer.",
            ["amuse.renderer.AdmittedMaterialSemanticsUnknown:description"] =
                "The materials resolved, but every triangle answer came " +
                "back unknown. The renderer keeps its original materials " +
                "and nothing was proven wrong.",
            ["amuse.renderer.AdmittedMaterialSemanticsUnknown:hint"] =
                "Check the material textures if you expected a split.",
            ["amuse.renderer.AdditiveLayerWithProofRelevantMaterialProperty:hint"] =
                "Move the material-property curves out of the additive " +
                "layer.",

            ["amuse.renderer.UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty"] =
                "This avatar has a blend tree that touches this renderer " +
                "in a way AMUSE cannot prove.",
            ["amuse.renderer.UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty:description"] =
                "A direct blend tree without normalized weights changes " +
                "material properties in ways AMUSE cannot add up. The " +
                "renderer keeps its original materials.",
            ["amuse.renderer.UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty:hint"] =
                "Normalize the blend tree weights, or move the curves out " +
                "of the direct blend tree.",

            ["amuse.renderer.AnimatedPropertyAbsentFromAdmittedMaterial"] =
                "An animation changes a property that this renderer's " +
                "materials do not have.",
            ["amuse.renderer.AnimatedPropertyAbsentFromAdmittedMaterial:description"] =
                "AMUSE cannot prove what the renderer shows when an " +
                "animation reaches for a property the materials do not " +
                "declare. The renderer keeps its original materials.",
            ["amuse.renderer.AnimatedPropertyAbsentFromAdmittedMaterial:hint"] =
                "Clean up the animation curves that point at missing " +
                "properties.",

            // --- Avatar-scoped animation refusals ---
            ["amuse.avatar.UnsupportedAnimatorControllerForm"] =
                "This avatar has an animator controller AMUSE cannot read.",
            ["amuse.avatar.UnsupportedAnimatorControllerForm:description"] =
                "One controller on this avatar is not a form AMUSE can " +
                "read. The avatar keeps all its original materials.",
            ["amuse.avatar.UnsupportedAnimatorControllerForm:hint"] =
                "Check the animator controllers on this avatar.",

            ["amuse.avatar.UnsupportedSyncedLayerOverrides"] =
                "This avatar has synced animation layers.",
            ["amuse.avatar.UnsupportedSyncedLayerOverrides:description"] =
                "A controller on this avatar shares one layer with other " +
                "layers. AMUSE cannot follow the shared layer. The avatar " +
                "keeps all its original materials.",
            ["amuse.avatar.UnsupportedSyncedLayerOverrides:hint"] =
                "Remove the synced layers if you want AMUSE to run on " +
                "this avatar.",

            ["amuse.avatar.UnresolvedVirtualizedMotionContext"] =
                "This avatar has a motion AMUSE cannot resolve.",
            ["amuse.avatar.UnresolvedVirtualizedMotionContext:description"] =
                "A controller on this avatar uses a motion that stays " +
                "unprepared at this build stage. The avatar keeps all its " +
                "original materials.",
            ["amuse.avatar.UnresolvedVirtualizedMotionContext:hint"] =
                "Update NDMF and the tools that build this avatar, then " +
                "run the build again.",

            ["amuse.avatar.UnrecognizedStateMachineBehaviour"] =
                "This avatar has a state behaviour AMUSE does not know.",
            ["amuse.avatar.UnrecognizedStateMachineBehaviour:description"] =
                "A state in an animator controller carries a behaviour " +
                "that AMUSE does not know. AMUSE cannot prove what that " +
                "behaviour does. The avatar keeps all its original " +
                "materials.",
            ["amuse.avatar.UnrecognizedStateMachineBehaviour:hint"] =
                "Remove the unknown behaviour if you do not need it, or " +
                "accept that this avatar stays unchanged.",

            ["amuse.avatar.AnimationEventPresent"] =
                "This avatar has animation events.",
            ["amuse.avatar.AnimationEventPresent:description"] =
                "An animation on this avatar calls a method by name. " +
                "AMUSE cannot prove what that call changes. The avatar " +
                "keeps all its original materials.",
            ["amuse.avatar.AnimationEventPresent:hint"] =
                "Remove the animation events if you want AMUSE to run on " +
                "this avatar.",

            // --- Host lifecycle refusals ---
            ["amuse.host.UnsupportedUnityVersion"] =
                "This Unity version is not supported.",
            ["amuse.host.UnsupportedUnityVersion:description"] =
                "AMUSE supports Unity 2022.3.22f1 or a newer 2022.3 " +
                "release. This build ran on a different version, so AMUSE " +
                "did nothing.",
            ["amuse.host.UnsupportedUnityVersion:hint"] =
                "Open the project in a supported Unity version.",

            ["amuse.host.UnsupportedNdmfVersion"] =
                "This NDMF version is not supported.",
            ["amuse.host.UnsupportedNdmfVersion:description"] =
                "AMUSE supports NDMF 1.14.4 or newer inside the 1.x line. " +
                "This build ran on a different version, so AMUSE did " +
                "nothing.",
            ["amuse.host.UnsupportedNdmfVersion:hint"] =
                "Update NDMF through the package manager.",

            ["amuse.host.UnsupportedVrchatSdkBaseVersion"] =
                "This VRChat SDK Base version is not supported.",
            ["amuse.host.UnsupportedVrchatSdkBaseVersion:description"] =
                "AMUSE supports VRChat SDK Base 3.10.4 or newer inside the " +
                "3.x line. This build ran on a different version, so AMUSE " +
                "did nothing.",
            ["amuse.host.UnsupportedVrchatSdkBaseVersion:hint"] =
                "Update the VRChat SDK through the package manager.",

            ["amuse.host.UnsupportedVrchatSdkAvatarsVersion"] =
                "This VRChat SDK Avatars version is not supported.",
            ["amuse.host.UnsupportedVrchatSdkAvatarsVersion:description"] =
                "AMUSE supports VRChat SDK Avatars 3.10.4 or newer inside " +
                "the 3.x line. This build ran on a different version, so " +
                "AMUSE did nothing.",
            ["amuse.host.UnsupportedVrchatSdkAvatarsVersion:hint"] =
                "Update the VRChat SDK through the package manager.",

            ["amuse.host.UnsupportedPlatform"] =
                "This avatar is not a VRChat avatar.",
            ["amuse.host.UnsupportedPlatform:description"] =
                "AMUSE 0.1 only runs on VRChat avatars.",
            ["amuse.host.UnsupportedPlatform:hint"] =
                "Remove the AMUSE component if this object is not an " +
                "avatar.",

            ["amuse.host.UnsupportedBuildPath"] =
                "AMUSE could not tell what kind of build this is.",
            ["amuse.host.UnsupportedBuildPath:description"] =
                "The build pipeline did not say whether this is an " +
                "upload or a Play mode run, so AMUSE changed nothing.",
            ["amuse.host.UnsupportedBuildPath:hint"] =
                "Update NDMF and run the build again.",

            ["amuse.host.MissingBuildContextServices"] =
                "The build pipeline is missing services AMUSE needs.",
            ["amuse.host.MissingBuildContextServices:description"] =
                "NDMF did not offer the asset saver, object registry, or " +
                "error report that AMUSE requires, so AMUSE did nothing.",
            ["amuse.host.MissingBuildContextServices:hint"] =
                "Check that NDMF is up to date and no other plugin broke " +
                "the build pipeline.",

            // --- Consent (D8 layer) ---
            ["amuse.consent.Declined"] =
                "You declined the unverified-version warning.",
            ["amuse.consent.Declined:description"] =
                "AMUSE changed nothing on this avatar. The versions you " +
                "have not verified stay untested until you accept the " +
                "warning during an upload.",
            ["amuse.consent.Declined:hint"] =
                "Update the listed packages to versions AMUSE has " +
                "verified, or accept the warning on the next upload.",

            // --- Texture capture refusals ---
            ["amuse.texture.UnavailableCapture"] =
                "AMUSE could not read one material texture.",
            ["amuse.texture.UnavailableCapture:description"] =
                "The capture of one texture on a material slot refused, so " +
                "AMUSE has no proof for the triangles that sample it. Those " +
                "triangles stay on the original material.",
            ["amuse.texture.UnavailableCapture:hint"] =
                "Check that the texture is a real imported asset and that " +
                "the project supports texture capture.",
            ["amuse.texture.NonResidentMips"] =
                "One material texture is missing mipmap levels.",
            ["amuse.texture.NonResidentMips:description"] =
                "The texture's mipmap limit removes levels the proof must " +
                "read, so AMUSE has no proof for the triangles that sample " +
                "it. Those triangles stay on the original material.",
            ["amuse.texture.NonResidentMips:hint"] =
                "Lower the project's texture mipmap limit, or remove the " +
                "texture's own limit.",
            ["amuse.texture.UnsupportedFormat"] =
                "One material texture uses a format AMUSE cannot read.",
            ["amuse.texture.UnsupportedFormat:description"] =
                "The texture's storage format is outside the formats AMUSE " +
                "can prove, so AMUSE has no proof for the triangles that " +
                "sample it. Those triangles stay on the original material.",
            ["amuse.texture.UnsupportedFormat:hint"] =
                "Re-import the texture as RGBA32, ARGB32, Alpha8, RGB24, " +
                "DXT5, or BC7.",

            // --- Avatar summary ---
            ["amuse.summary.Title"] =
                "AMUSE finished this avatar.",
            ["amuse.summary.Title:description"] =
                "AMUSE analyzed {0} renderers and moved {1} triangles to " +
                "opaque materials. {2} renderers kept everything original.",
            ["amuse.summary.Title:hint"] =
                "Open this component to see the same status.",
            ["amuse.summary.PolicyActive:description"] =
                "AMUSE moved triangles while the alpha policy was " +
                "active, so some moved triangles rest on your alpha " +
                "settings rather than on exact proof.",
        };

        /// <summary>True when the table holds a string for the key.</summary>
        internal static bool Has(string key)
        {
            return Strings.ContainsKey(key);
        }

        /// <summary>The plain English string, or an empty string.</summary>
        internal static string Get(string key)
        {
            return Strings.TryGetValue(key, out var value) ? value : "";
        }

        internal static string RendererKey(RendererAnalysisRefusal cause)
        {
            return Prefix + "renderer." + cause;
        }


        internal static string AvatarKey(AvatarAnimationRefusal cause)
        {
            return Prefix + "avatar." + cause;
        }
        internal static string HostKey(HostLifecycleRefusal cause)
        {
            return Prefix + "host." + cause;
        }
        /// <summary>
        /// The report key for one texture capture refusal family. The slot
        /// resolution carries the refusal records; a report names the exact
        /// family through this key, in the shape every other refusal
        /// vocabulary uses.
        /// </summary>
        internal static string TextureCaptureKey(
            TextureCaptureRefusalReason cause)
        {
            return Prefix + "texture." + cause;
        }
    }
}
