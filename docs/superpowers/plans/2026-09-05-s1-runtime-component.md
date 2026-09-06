# S1 runtime assembly and component — slice plan

> **For agentic workers:** Bite-sized RED/GREEN tasks. Execute in order in the
> `feature/s1-runtime-component` worktree. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship the opt-in avatar-root component: a runtime assembly, the
component type, a testable placement rule, and a plain English inspector.

**Architecture:** A new `Runtime` assembly (all platforms) holds the component
so Unity keeps it in avatar builds long enough for NDMF to see it. The
component implements NDMF's `INDMFEditorOnly`, which derives
`VRC.SDKBase.IEditorOnly` when the SDK is present, so the component carries no
direct SDK reference. A static placement rule is the test seam. The editor
assembly references the runtime assembly and draws guidance. The build-side
authoritative root check arrives with S2; the NDMF avatar root is the
authority there.

**Tech Stack:** Unity 2022.3 APIs, NDMF 1.14.x runtime, NUnit via the Unity
Test Runner (EditMode).

**Spec:** `docs/superpowers/specs/2026-09-05-0.1.0-scope-design.md` V1.
Roadmap: `docs/superpowers/plans/2026-09-05-0.1.0-implementation-roadmap.md`.

## Global Constraints

- One public type per file. File name equals type name. Namespace mirrors
  folder: `Alrauna.Amuse.Runtime`, `Alrauna.Amuse.Editor`.
- No new dependencies. No direct VRChat SDK reference.
- Plain English strings: short sentences, active voice, no contractions.
- Tests: NUnit `Assert.That`. One test class per production type. A filtered
  run reporting 0 tests is a failure. Record observed counts.

## File Structure

- Create `Packages/com.alrauna.amuse/Runtime/Alrauna.Amuse.Runtime.asmdef`
- Create `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`
- Create `Packages/com.alrauna.amuse/Runtime/AmuseComponentPlacement.cs`
- Create `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Modify `Packages/com.alrauna.amuse/Editor/Alrauna.Amuse.Editor.asmdef`
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef`
- Create `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseComponentPlacementTests.cs`
- Create `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`

---

### Task 1: runtime assembly, placement stub, failing tests

**Interfaces:**
- Produces: `Alrauna.Amuse.Runtime.AmuseComponentPlacement.IsOnHierarchyRoot(Component) : bool`
- Produces: asmdef `Alrauna.Amuse.Runtime` (empty `includePlatforms`,
  references `nadena.dev.ndmf.runtime`, `autoReferenced: true`)

- [ ] **Step 1: Write the runtime asmdef**

```json
{
    "name": "Alrauna.Amuse.Runtime",
    "rootNamespace": "Alrauna.Amuse.Runtime",
    "references": [
        "nadena.dev.ndmf.runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Write the placement stub. The stub is the named wrong
  implementation "a component is accepted anywhere".**

```csharp
using UnityEngine;

namespace Alrauna.Amuse.Runtime
{
    /// <summary>
    /// Placement rules for AmuseAvatarOptimizer. The component must sit on the
    /// root of the avatar hierarchy. A parent transform means a misplaced
    /// component. The build gate re-checks placement against the NDMF avatar
    /// root, which is the authority.
    /// </summary>
    public static class AmuseComponentPlacement
    {
        /// <summary>True when the component sits on a hierarchy root.</summary>
        public static bool IsOnHierarchyRoot(Component component)
        {
            return true;
        }
    }
}
```

- [ ] **Step 3: Write the failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class AmuseComponentPlacementTests
    {
        [Test]
        public void ComponentWithoutParentIsOnHierarchyRoot()
        {
            var root = new GameObject("placement-root");
            var component = root.AddComponent<AmuseAvatarOptimizerStubHolder>();
            Assert.That(AmuseComponentPlacement.IsOnHierarchyRoot(component), Is.True);
        }

        [Test]
        public void ComponentWithParentIsRefused()
        {
            var root = new GameObject("placement-parent");
            var child = new GameObject("placement-child");
            child.transform.SetParent(root.transform);
            var component = child.AddComponent<AmuseAvatarOptimizerStubHolder>();
            Assert.That(AmuseComponentPlacement.IsOnHierarchyRoot(component), Is.False);
        }

        [Test]
        public void NullComponentIsRefused()
        {
            Assert.That(AmuseComponentPlacement.IsOnHierarchyRoot(null), Is.False);
        }

        private sealed class AmuseAvatarOptimizerStubHolder : MonoBehaviour
        {
        }
    }
}
```

Note: Task 1 uses a private MonoBehaviour stub because the real component type
arrives in Task 3. Task 3 replaces the stub usage with the real component.

- [ ] **Step 4: Add the runtime reference to the test asmdef**

`references` in `Alrauna.Amuse.Tests.Editor.asmdef` gains
`"Alrauna.Amuse.Runtime"` after `"Alrauna.Amuse.Editor"`.

- [ ] **Step 5: Refresh Unity, run the two placement tests, observe RED**

Run: Unity Test Runner filter `AmuseComponentPlacementTests`. Expected:
`ComponentWithParentIsRefused` FAILS (stub accepts anywhere) and
`NullComponentIsRefused` FAILS. `ComponentWithoutParentIsOnHierarchyRoot`
passes against the stub. Record counts.

- [ ] **Step 6: Commit the RED state**

```bash
git add Packages/com.alrauna.amuse/Runtime Packages/com.alrauna.amuse/Tests
git commit -m "wip(s1): placement rule stub with failing tests"
```

### Task 2: make placement refuse misplaced components

- [ ] **Step 1: Replace the stub body**

```csharp
public static bool IsOnHierarchyRoot(Component component)
{
    return component != null && component.transform.parent == null;
}
```

- [ ] **Step 2: Re-run the placement tests. Expected: 3 of 3 PASS. Record counts.**

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Runtime/AmuseComponentPlacement.cs
git commit -m "feat(s1): refuse misplaced component placement"
```

### Task 3: the component type

**Interfaces:**
- Produces: `Alrauna.Amuse.Runtime.AmuseAvatarOptimizer : MonoBehaviour, INDMFEditorOnly`

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;
using nadena.dev.ndmf;

namespace Alrauna.Amuse.Runtime
{
    /// <summary>
    /// Opts an avatar into AMUSE build-time optimization. Presence on the
    /// avatar root turns the pipeline on. Absence keeps the build untouched.
    /// NDMF removes IEditorOnly components from the uploaded avatar, so this
    /// component never ships.
    /// </summary>
    [AddComponentMenu("AMUSE/Avatar Optimizer")]
    [DisallowMultipleComponent]
    [HelpURL("https://github.com/Alrauna/AMUSE")]
    public sealed class AmuseAvatarOptimizer : MonoBehaviour, INDMFEditorOnly
    {
    }
}
```

- [ ] **Step 2: Write the component tests, replacing the Task 1 stub usage**

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using nadena.dev.ndmf;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class AmuseAvatarOptimizerTests
    {
        // Characterization on creation: the VRChat SDK strips avatar
        // components that lack the editor-only marker. This assertion pins
        // the marker because a missing marker ships the component to VRChat.
        [Test]
        public void ComponentCarriesEditorOnlyMarker()
        {
            var interfaces = typeof(AmuseAvatarOptimizer).GetInterfaces();
            Assert.That(interfaces, Does.Contain(typeof(INDMFEditorOnly)));
        }

        [Test]
        public void ComponentAppearsUnderAmuseMenu()
        {
            var attribute = typeof(AmuseAvatarOptimizer)
                .GetCustomAttributes(typeof(AddComponentMenu), false)
                .Cast<AddComponentMenu>()
                .Single();
            Assert.That(attribute.componentMenu, Is.EqualTo("AMUSE/Avatar Optimizer"));
        }
    }
}
```

`ComponentAppearsUnderAmuseMenu` pins the user-facing menu path; the menu is
the component's public name. The placement tests from Task 1 now use
`AmuseAvatarOptimizer` instead of the stub holder; delete
`AmuseAvatarOptimizerStubHolder`. Re-run the placement tests too.

- [ ] **Step 3: Refresh Unity, run both test classes. Expected: 5 of 5 PASS
  (2 placement refusals already GREEN, root case, marker, menu). Record counts.**

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Runtime Packages/com.alrauna.amuse/Tests
git commit -m "feat(s1): add opt-in avatar component with editor-only marker"
```

### Task 4: inspector with plain English guidance

- [ ] **Step 1: Add the runtime reference to the editor asmdef**

`references` in `Alrauna.Amuse.Editor.asmdef` gains
`"Alrauna.Amuse.Runtime"` after `"nadena.dev.ndmf.runtime"`.

- [ ] **Step 2: Write the inspector**

```csharp
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Inspector for AmuseAvatarOptimizer. Shows placement guidance. The last
    /// build status arrives with the report channel slice.
    /// </summary>
    [CustomEditor(typeof(AmuseAvatarOptimizer))]
    public sealed class AmuseAvatarOptimizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var component = (AmuseAvatarOptimizer)target;
            if (AmuseComponentPlacement.IsOnHierarchyRoot(component))
            {
                EditorGUILayout.HelpBox(
                    "AMUSE will run on this avatar at upload. " +
                    "It moves proven opaque parts of transparent materials onto opaque copies. " +
                    "Anything it cannot prove stays unchanged and gets reported.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This component must sit on the root object of the avatar. " +
                    "Move it to the top object. " +
                    "The optimizer does not run while the component sits on a child.",
                    MessageType.Error);
            }
        }
    }
}
```

- [ ] **Step 3: Refresh Unity. Expected: compile clean. Manual smoke: select a
  component on a child, see the error box; move it to the root, see the info
  box. Tests cannot observe the inspector.**

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor
git commit -m "feat(s1): add component inspector with placement guidance"
```

### Task 5: full assembly run and ship

- [ ] **Step 1: Run the full `Alrauna.Amuse.Tests.Editor` assembly. Expected:
  all prior counts hold plus the 5 new tests. Record the counts.**
- [ ] **Step 2: Commit the Unity-generated `.meta` files for every new file
  and folder. Treat asset and meta as one unit.**
- [ ] **Step 3: Push, open the PR, enable auto-merge. PR body records the
  observed RED and GREEN counts.**
