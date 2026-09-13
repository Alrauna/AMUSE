using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Fixture identity helper. Installed optimizer packages resolve an
    /// avatar's host platform from its components: without a
    /// VRCAvatarDescriptor, Avatar Optimizer's build treats the fixture as a
    /// Generic avatar and its garbage collector strips the root Animator,
    /// which destroys the animation evidence the production passes must
    /// observe. Test fixtures therefore carry the descriptor, exactly as
    /// production avatars do.
    /// <para>
    /// The descriptor type lives in the VRChat SDK precompiled DLLs. The
    /// test assembly references those DLLs and a version define bound to
    /// com.vrchat.avatars gates every use, so the assembly still compiles
    /// on a fresh clone without the SDK. There a descriptor-dependent
    /// fixture reports a loud ignore naming the missing package instead of
    /// a silent pass.
    /// </para>
    /// </summary>
    internal static class FixtureAvatarIdentity
    {
        /// <summary>
        /// Attaches the VRChat avatar descriptor to the fixture root, so
        /// every installed build plugin resolves the same VRChat platform
        /// production resolves. Call once per fixture root before the
        /// build. Without the SDK this ignores the running test loudly,
        /// naming the missing package.
        /// </summary>
        internal static void AttachVrcDescriptor(GameObject root)
        {
            RequireVrcDescriptorSupport();
#if AMUSE_VRCSDK3_AVATARS
            root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
        }

        /// <summary>
        /// Guards a test whose fixture needs the descriptor. Without the
        /// SDK the fixture cannot reproduce the production platform
        /// resolution, so the test ignores loudly instead of measuring a
        /// different environment.
        /// </summary>
        internal static void RequireVrcDescriptorSupport()
        {
#if !AMUSE_VRCSDK3_AVATARS
            const string SdkPackage = "com.vrchat.avatars";
            Assert.Ignore(
                "The " + SdkPackage + " package is not installed in this"
                + " project. Fixture avatars cannot carry a"
                + " VRCAvatarDescriptor, so installed optimizer packages"
                + " resolve them as Generic avatars and strip the animation"
                + " state these tests observe. Install " + SdkPackage
                + " to run them.");
#endif
        }
    }
}
