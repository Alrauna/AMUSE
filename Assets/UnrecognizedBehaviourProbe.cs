#if UNITY_EDITOR
using UnityEngine;

namespace Alrauna.Amuse.TestFixtures
{
    /// <summary>
    /// A synthetic StateMachineBehaviour that AMUSE does not recognize.
    /// AMUSE must refuse it and record its identity. It must stay in
    /// Assembly-CSharp. An Editor-assembly behaviour does not attach
    /// reliably through AddStateMachineBehaviour on Unity 2022.3.22f1.
    /// PackageInfo.FindForAssembly must return null for its assembly, so
    /// identity tests exercise the exact package-less representation.
    /// Its full name collides with a stand-in class in the test assembly,
    /// and identity must still separate the two.
    /// </summary>
    public sealed class UnrecognizedBehaviourProbe : StateMachineBehaviour
    {
    }
}
#endif
