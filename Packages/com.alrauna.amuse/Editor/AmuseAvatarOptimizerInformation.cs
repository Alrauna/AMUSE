#if AVATAR_OPTIMIZER && UNITY_EDITOR
using Alrauna.Amuse.Runtime;
using Anatawa12.AvatarOptimizer.API;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Registers the AMUSE component with Avatar Optimizer's component
    /// registry. Without this registration, Avatar Optimizer's Trace and
    /// Optimize treats the component as an unknown component type and
    /// warns; its assumptions about unknown components do not hold for a
    /// build-time component whose passes run at platform finish, after
    /// Avatar Optimizer has processed the avatar.
    /// <para>
    /// The component serializes scalars only, so the unknown-component
    /// assumption that an unknown component references components worth
    /// keeping is vacuous here: registration declares nothing kept alive
    /// except the component itself. It is declared a dependency of the
    /// avatar root, even when disabled, because the build gate reads the
    /// serialized disable toggle and never the component's enabled state;
    /// this preserves the pre-registration behavior, where Avatar
    /// Optimizer kept every unknown component.
    /// </para>
    /// <para>
    /// No mutations are declared: Avatar Optimizer processes before the
    /// AMUSE passes run, so its own passes need no bookkeeping for what
    /// AMUSE changes afterwards. Guarded by the version define bound to
    /// <c>com.anatawa12.avatar-optimizer [1.7,2.0)</c>; the API surface is
    /// declared unstable by the vendor across major versions.
    /// </para>
    /// </summary>
    [ComponentInformation(typeof(AmuseAvatarOptimizer))]
    internal sealed class AmuseAvatarOptimizerInformation :
        ComponentInformation<AmuseAvatarOptimizer>
    {
        protected override void CollectDependency(
            AmuseAvatarOptimizer component,
            ComponentDependencyCollector collector)
        {
            collector.AddDependency(component.transform.root, component)
                .EvenIfDependantDisabled();
        }
    }
}
#endif
