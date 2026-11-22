namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The avatar-scope and use-scope refusals of the atlas pass.
    /// <see cref="None"/> is not a refusal: it means no atlas gate
    /// has refused yet. Design section 4 conditions 1 and 2.
    /// </summary>
    internal enum AtlasUseRefusal
    {
        None,

        /// <summary>The build target is not the Windows desktop target.</summary>
        UnsupportedBuildTarget,

        /// <summary>The maximum page size left the range 1 to 8192.</summary>
        MaximumPageSizeOutOfRange,

        /// <summary>The protected mip level count left the range 1 to 14.</summary>
        ProtectedMipLevelCountOutOfRange,

        /// <summary>The atlas pass could not enumerate the committed animation graph.</summary>
        AnimationGraphUnavailable,

        /// <summary>The renderer is neither a mesh renderer nor a skinned mesh renderer.</summary>
        UnsupportedRenderer,

        /// <summary>The renderer has no mesh.</summary>
        MissingMesh,

        /// <summary>The slot has no material.</summary>
        MissingMaterial,

        /// <summary>An animation changes the mesh or the slot count of the renderer.</summary>
        AnimatedRendererShape,

        /// <summary>An animation swaps the material of the slot.</summary>
        AnimatedMaterialSlot,

        /// <summary>The shader of the slot matches no admitted row.</summary>
        UnattestedShader,

        /// <summary>The material carries a recognized vendor lock.</summary>
        LockedShader,

        /// <summary>The admitted row has no route table.</summary>
        NoRouteTable,

        /// <summary>An enabled local feature keyword is outside the allowed list.</summary>
        KeywordNotAllowed,

        /// <summary>A required value of the route does not hold.</summary>
        RequiredValueNotHeld,

        /// <summary>An assigned texture is neither a member nor neutral.</summary>
        UnlistedTexture,

        /// <summary>A property block entry targets a route or condition property.</summary>
        PropertyBlockOnRouteProperty,

        /// <summary>An animated property targets a route or condition property.</summary>
        AnimatedRouteProperty,

        /// <summary>The member texture is missing.</summary>
        MissingMainTexture,

        /// <summary>The member texture is not a 2D texture.</summary>
        UnsupportedTextureDimension,

        /// <summary>The member texture is not an asset on disk.</summary>
        SourceNotAnAsset,

        /// <summary>The member texture has a format the page cannot copy.</summary>
        UnsupportedFormat,

        /// <summary>The member texture wraps in a mode the page cannot border.</summary>
        UnsupportedWrap,

        /// <summary>The member transform is not identity.</summary>
        NonIdentityTransform,

        /// <summary>The mesh has no UV0 channel.</summary>
        MissingRouteChannel,

        /// <summary>A UV value of the use lies outside the unit square.</summary>
        UvOutsideUnitSquare,

        /// <summary>The source size at a protected level is not block aligned.</summary>
        SourceNotAligned,

        /// <summary>The source mip chain is shorter than the protected level count.</summary>
        SourceMipChainTooShort,

        /// <summary>A BC7 source needs a clamp edge border.</summary>
        ClampEdgeOnBc7,

        /// <summary>A placement does not fit a page of the maximum size.</summary>
        PlacementTooLarge,

        /// <summary>The group retained bytes exceed the group limit.</summary>
        GroupBudget,

        /// <summary>The avatar retained bytes exceed the avatar limit.</summary>
        AvatarBudget,

        /// <summary>The predicted peak bytes exceed the peak limit.</summary>
        PeakBudget,

        /// <summary>A source of the group could not be acquired.</summary>
        SourceAcquisitionFailed,

        /// <summary>Another slot draws the same submesh as this slot.</summary>
        SlotSharesSubmesh,

        /// <summary>The submesh of the use is not made of triangles.</summary>
        NonTriangleTopology,

        /// <summary>The submesh of the use has no triangles.</summary>
        EmptySubmesh,

        /// <summary>
        /// Every admitted use of the group shares one source set. A
        /// page would only copy it.
        /// </summary>
        SingleSourceGroup,

        /// <summary>
        /// A companion route texture size is not the main size divided
        /// by one power of two on both axes.
        /// </summary>
        CompanionSizeMismatch,

        /// <summary>
        /// An outline row reads a companion with <c>sampler_MainTex</c>,
        /// and the outline texture sampler differs from the main
        /// texture sampler.
        /// </summary>
        RouteSamplerMismatch,
    }
}
