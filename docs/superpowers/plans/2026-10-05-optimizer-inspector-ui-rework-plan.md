# Optimizer Inspector UI Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename and regroup the optimizer inspector, and add an on-by-default "Alpha Separator" feature switch that silently stops the alpha separation pipeline when off.

**Architecture:** One new serialized bool on the runtime component, one silent gate in the barrier pass right after the V1 trigger, and an inspector restructure that moves "Disable AMUSE" into a renamed "Settings" foldout and hides the alpha policy foldout behind a non-persisted "Advanced Settings" reveal toggle.

**Tech Stack:** Unity 2022.3 editor C#, NDMF 1.14.x, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-10-05-optimizer-inspector-ui-rework-design.md`

**Working discipline:** Base branch `feature/optimizer-ui-rework` at `f319186`, cut from `main`. Allowed mutations: the four files named below plus `README.md`. The uncommitted change to `.github/workflows/pr.yml` is user-owned. Do not stage, commit, or push without explicit session authorization. Stop and return evidence when a gate placement contradicts the characterization in the plan.

## Global Constraints

- Production code is editor-only except the runtime component assembly. Never mutate Unity source assets.
- Labels and tooltips use plain technical English. Short active sentences. No contractions.
- XML doc comments state why, not what.
- RED/GREEN: observe the failing test first against a named wrong implementation. A test that passes on first run is recorded as characterization.
- A filtered test run that reports 0 tests is a failure. Record observed counts.
- Unity MCP safety, before any tool use on the editor: read `mcpforunity://instances`; inspect `Application.dataPath` of the candidate; require an exact match to the repository `Assets` folder; pin the instance when more than one is connected. Tests never run in the Census Lab project.
- No absolute or machine-specific paths in code, comments, docs, or commit messages.

---

### Task 1: Runtime switch and characterization tests

**Files:**
- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: serialized field `_alphaSeparatorEnabled` (bool, default `true`) and property `bool AlphaSeparatorEnabled` on `AmuseAvatarOptimizer`. Tasks 2, 3, and 4 read these names.

- [ ] **Step 1: Add the characterization tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`, after `IgnoreOutOfRangeMaterialSlotsSerializedPropertyCanBeToggled` (ends near line 66), add:

```csharp
        [Test]
        public void DefaultAlphaSeparatorEnabledIsTrue()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(optimizer.AlphaSeparatorEnabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AlphaSeparatorEnabledSerializedPropertyCanBeToggled()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(optimizer.AlphaSeparatorEnabled, Is.True);

                var serializedObject = new SerializedObject(optimizer);
                var property =
                    serializedObject.FindProperty("_alphaSeparatorEnabled");
                Assert.That(property, Is.Not.Null);
                property.boolValue = false;
                serializedObject.ApplyModifiedProperties();

                Assert.That(optimizer.AlphaSeparatorEnabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
```

- [ ] **Step 2: Add the field and property**

In `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`, after `_amuseDisabled` (line 18), add:

```csharp
        /// <summary>
        /// The alpha separator switch. The alpha separator is the
        /// first AMUSE feature with its own switch, and later features
        /// add sibling switches beside it. The master "Disable AMUSE"
        /// toggle stays above them and turns off the whole component.
        /// Off here stops the alpha separator only: nothing is
        /// analyzed, moved, or reported for it.
        /// </summary>
        [SerializeField]
        private bool _alphaSeparatorEnabled = true;
```

After the `AmuseDisabled` property (ends near line 106), add:

```csharp
        /// <summary>
        /// True when the alpha separator runs for this component. The
        /// default of true keeps every avatar saved before this field
        /// behaving as before, because Unity fills a missing serialized
        /// field from the initializer.
        /// </summary>
        public bool AlphaSeparatorEnabled => _alphaSeparatorEnabled;
```

- [ ] **Step 3: Compile and run the two tests**

Refresh the dev editor instance, confirm zero compile errors, then run the EditMode filter `DefaultAlphaSeparatorEnabledIsTrue;AlphaSeparatorEnabledSerializedPropertyCanBeToggled`.
Expected: 2 tests pass. Record the run as characterization: both assertions pass on first run because the field lands with them.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs
git commit -m "feat: add the alpha separator switch to the optimizer component"
```

---

### Task 2: Barrier gate, RED then GREEN

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: `_alphaSeparatorEnabled` and `AlphaSeparatorEnabled` from Task 1.
- Produces: private static `bool AlphaSeparatorSwitchedOff(BuildContext context)` on `AmusePlatformFinishPlugin`. Nothing outside the plugin reads it.

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, after `DisabledComponentDoesNotActivateThePipeline` (ends near line 277), add:

```csharp
        [Test]
        public void AlphaSeparatorSwitchedOffDoesNotActivateThePipeline()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE alpha-switch-off fixture");
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            var component =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            root.AddComponent<LineRenderer>();

            try
            {
                var serialized = new UnityEditor.SerializedObject(component);
                serialized.FindProperty("_alphaSeparatorEnabled")
                    .boolValue = false;
                serialized.ApplyModifiedProperties();

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                AmusePlatformFinishPass.Execute(context, SupportedFacts());

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero,
                    "a switched-off alpha separator must not activate"
                    + " the pipeline");
                Assert.That(amuse.SemanticallyRefusedRendererCount,
                    Is.Zero,
                    "a switched-off alpha separator must not turn the"
                    + " loop into refusals");
                Assert.That(amuse.ReachedRendererAnalysis, Is.False,
                    "a switched-off alpha separator must stop before"
                    + " the renderer loop");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
```

- [ ] **Step 2: Run it and observe RED**

Run the EditMode filter `AlphaSeparatorSwitchedOffDoesNotActivateThePipeline`.
Expected: FAIL. Named wrong implementation: the component is present and not
disabled, so the barrier runs and reaches renderer analysis. The
`ReachedRendererAnalysis` assertion trips first. Record the observed failure
text.
Fallback: if the test passes, an avatar-scope gate stopped the ungated run
before renderer analysis. That fixture cannot prove the gate. Replace the
`LineRenderer` with a renderer the loop admits, mirroring the skinned fixture
of `SerializedDisableControlKeepsTheBuildUntouched`, and observe RED again. A
RED that never fails is not evidence.

- [ ] **Step 3: Add the gate**

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, directly after the V1 trigger block (`if (!TriggerActivated(context)) { return; }`, ends at line 426) and before the D8 consent comment, add:

```csharp
            // Feature switch: the alpha separator runs only with its
            // toggle on. The component stays present for later sibling
            // features. Off mirrors the Disable AMUSE contract for this
            // one feature: nothing analyzed, nothing mutated, nothing
            // reported, and the consent layer below never asks.
            if (AlphaSeparatorSwitchedOff(context))
            {
                return;
            }
```

After `TriggerActivated` (ends at line 853), add:

```csharp
        /// <summary>
        /// True when the avatar root carries the optimizer with its
        /// alpha separator switch off. The barrier then stops between
        /// the V1 trigger and the consent layer, so the feature runs
        /// nothing and reports nothing while later sibling features
        /// keep their own switches on the same component.
        /// </summary>
        private static bool AlphaSeparatorSwitchedOff(BuildContext context)
        {
            var component =
                context.AvatarRootObject
                    .GetComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            return component != null && !component.AlphaSeparatorEnabled;
        }
```

- [ ] **Step 4: Run it and observe GREEN**

Run the EditMode filter `AlphaSeparatorSwitchedOffDoesNotActivateThePipeline`.
Expected: PASS. Also rerun `DisabledComponentDoesNotActivateThePipeline`.
Expected: PASS, 1 test.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: gate the alpha separation barrier on the optimizer switch"
```

---

### Task 3: Inspector restructure

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`

**Interfaces:**
- Consumes: `_alphaSeparatorEnabled` from Task 1.
- Produces: no API. Verification is compile, suite, and an editor smoke check.

- [ ] **Step 1: Rename the state fields**

Replace the two private fields near lines 16-17:

```csharp
        private bool _settingsOpen;
        private bool _advancedSettingsRevealed;
        private bool _alphaSeparatorOpen;
```

`_advancedOpen` becomes `_settingsOpen`. `_advancedSettingsRevealed` is new and defaults false.

- [ ] **Step 2: Rework `OnInspectorGUI`**

Replace the block from `serializedObject.Update();` (line 42) through `DrawAdvancedSettings();` (line 51) with:

```csharp
            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_alphaSeparatorEnabled"),
                new GUIContent("Alpha Separator",
                    "Turn this off to skip alpha separation on this " +
                    "avatar. Nothing is analyzed, moved, or reported " +
                    "for the feature. A later feature adds its own " +
                    "switch beside this one."));
            serializedObject.ApplyModifiedProperties();

            DrawSettings();
            if (_advancedSettingsRevealed)
            {
                DrawAlphaSeparatorSettings();
            }
```

The placement help box above stays unchanged. The status box below stays unchanged.

- [ ] **Step 3: Replace `DrawAdvancedSettings` with `DrawSettings`**

Replace the whole `DrawAdvancedSettings` method (lines 300-343, including its doc comment) with:

```csharp
        /// <summary>
        /// The Settings foldout. It holds the master switch, the two
        /// tolerance consents, and the reveal toggle for the alpha
        /// policy menu. Every switch a user must find lives here, so
        /// one menu answers "what does AMUSE do on this avatar".
        /// </summary>
        private void DrawSettings()
        {
            _settingsOpen = EditorGUILayout.Foldout(
                _settingsOpen, "Settings", EditorStyles.foldoutHeader);
            if (!_settingsOpen)
            {
                return;
            }

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_amuseDisabled"),
                new GUIContent("Disable AMUSE",
                    "Treats this component as absent: nothing runs on " +
                    "build, in Play mode, or anywhere else, and nothing " +
                    "is reported."));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots"),
                new GUIContent(
                    "Ignore Out-of-Range Material Slots",
                    "Some animation files animate material slots that do not exist on this mesh. " +
                    "By default, AMUSE safely ignores these extra slot animations and optimizes the valid slots. " +
                    "Turn this setting off to refuse meshes with extra slot animations."));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_allowDepthTestChange"),
                new GUIContent(
                    "Allow Depth Test Change on Moved Triangles",
                    "Some materials set a special depth rule: draw a " +
                    "pixel only when it is strictly closer than " +
                    "everything already drawn. When AMUSE moves solid " +
                    "triangles of such a material onto an opaque copy, " +
                    "those triangles use the normal depth rule, which " +
                    "also draws pixels at the same distance. On rare " +
                    "layered parts, surfaces at exactly the same " +
                    "distance can swap their draw order or flicker." +
                    "\n\n" +
                    "By default, AMUSE accepts this stated change and " +
                    "moves the triangles. Turn this setting off to keep " +
                    "materials with a special depth rule on their " +
                    "original material."));

            _advancedSettingsRevealed = EditorGUILayout.Toggle(
                new GUIContent("Advanced Settings",
                    "Shows the Alpha Separator Settings menu below. " +
                    "Most users never need it. The menu hides again " +
                    "after Unity reloads scripts."),
                _advancedSettingsRevealed);
            serializedObject.ApplyModifiedProperties();
        }
```

- [ ] **Step 4: Rename the alpha foldout**

In `DrawAlphaSeparator` (lines 60-81): rename the method to `DrawAlphaSeparatorSettings`, change the foldout label from `"Alpha Separator"` to `"Alpha Separator Settings"`, and replace the doc comment with:

```csharp
        /// <summary>
        /// The Alpha Separator Settings foldout. It holds the user's
        /// policy for the alpha separation feature: which texture
        /// levels the opacity proof consults, and how big a split must
        /// be before AMUSE pays a draw call for it. It stays hidden
        /// until the Advanced Settings reveal in Settings is on,
        /// because most users never change this policy.
        /// </summary>
```

The four control calls and the final `ApplyModifiedProperties` stay unchanged.

- [ ] **Step 5: Compile and run the full product suite**

Refresh the dev editor instance, confirm zero compile errors, then run the full `Alrauna.Amuse.Tests.Editor` EditMode suite.
Expected: 0 compile errors, 0 failures. Record the observed total.

- [ ] **Step 6: Editor smoke check**

Following the Unity MCP safety constraints, pin the dev editor instance. In the open scene, create a scratch GameObject with an `AmuseAvatarOptimizer` component. Confirm through the editor that the inspector shows: the "Alpha Separator" toggle checked on above the "Settings" foldout; "Disable AMUSE" at the top inside "Settings"; the "Advanced Settings" reveal toggle at the bottom of "Settings"; no "Alpha Separator Settings" menu until the reveal is on, then the menu below "Settings" with its four controls. Delete the scratch object. Report what was observed. If the Inspector panel cannot be observed through tooling, verify the compiled editor type carries the three private state fields and say explicitly that the visual check was not performed.

- [ ] **Step 7: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "feat: rework the optimizer inspector for per-feature switches"
```

---

### Task 4: Merged-consumption pin for the switch

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs`

**Interfaces:**
- Consumes: `_alphaSeparatorEnabled` from Task 1, the gate from Task 2, and the file's existing helpers `RequireIntegrationEnvironment`, `AttachAnimationFixture`, `Track`, `ImportBandedAlphaTexture`, `NewTransparentMaterial`, `CreateSkinnedRenderer`.
- Produces: no API.

- [ ] **Step 1: Add the end-to-end test**

After `SerializedDisableControlKeepsTheBuildUntouched` (ends near line 842), add:

```csharp
        /// <summary>
        /// The serialized alpha separator switch, set off before
        /// processing. The barrier gates the feature after the pass
        /// records execution, and every authored triangle stays on its
        /// original material.
        /// </summary>
        [Test]
        public void SerializedAlphaSeparatorSwitchKeepsTheBuildUntouched()
        {
            RequireIntegrationEnvironment(
                out var traceAndOptimizeType,
                out var transparentShader,
                out _);
            AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

            var root = new GameObject("AMUSE alpha switch control");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                var amuse = root.AddComponent<AmuseAvatarOptimizer>();
                var serialized = new SerializedObject(amuse);
                var toggle =
                    serialized.FindProperty("_alphaSeparatorEnabled");
                Assert.That(toggle, Is.Not.Null,
                    "AmuseAvatarOptimizer._alphaSeparatorEnabled field pin");
                toggle.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                AttachAnimationFixture(root, "alpha-switch");
                var texture = Track(ImportBandedAlphaTexture(
                    "banded_alpha_switch"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                CreateSkinnedRenderer(
                    root, "AMUSE alpha switch renderer",
                    "AMUSE alpha switch mesh",
                    new[] { transparent },
                    new[] { Band.Transparent, Band.Partial, Band.Opaque },
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0);

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmbientPlatform.DefaultPlatform);
                Assert.That(context, Is.Not.Null);
                Assert.That(
                    context.Successful, Is.True,
                    "a switched-off alpha separator must not fail the"
                    + " build");

                var state = context.GetState<AmusePlatformFinishState>();
                Assert.That(state, Is.Not.Null);
                Assert.That(
                    state.HasExecuted, Is.True,
                    "HasExecuted records that the barrier pass ran; the"
                    + " feature switch gates the pipeline after it");
                Assert.That(
                    state.AnalyzedRendererCount, Is.EqualTo(0),
                    "a switched-off run must not analyze renderers");

                var generated = new HashSet<Material>(
                    state.Separation?.CreatedClones
                    ?? (IEnumerable<Material>)Array.Empty<Material>());
                Assert.That(
                    generated, Is.Empty,
                    "a switched-off run must not create generated"
                    + " materials");

                foreach (var renderer in root.GetComponentsInChildren<
                             SkinnedMeshRenderer>(true))
                {
                    var mesh = renderer.sharedMesh;
                    if (mesh == null) continue;
                    Assert.That(
                        renderer.sharedMaterials,
                        Has.All.Matches<Material>(material =>
                            material == transparent),
                        "a switched-off run must leave the original"
                        + " material on every slot");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
```

- [ ] **Step 2: Run it**

Run the EditMode filter `SerializedAlphaSeparatorSwitchKeepsTheBuildUntouched`.
Expected: PASS with at least 1 test run. If `RequireIntegrationEnvironment` skips in this environment, record the skip reason and keep the test for environments that have the integration.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs
git commit -m "test: pin the alpha separator switch in the merged pipeline"
```

---

### Task 5: README note, full validation, hygiene

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: the finished Tasks 1-4.
- Produces: no API.

- [ ] **Step 1: Add the README bullet**

In `README.md`, directly after the "Disable AMUSE" bullet (line 23), add:

```markdown
- **Alpha Separator** is on by default. Turn it off to skip alpha separation for that avatar.
```

- [ ] **Step 2: Full validation**

Run both EditMode assemblies in full: `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`.
Expected: 0 failures in both. Record observed totals.

- [ ] **Step 3: Hygiene**

Run `git diff --check` on the branch. Sweep every changed doc for identifiers: an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports. Every hit is a defect. Confirm the user-owned change to `.github/workflows/pr.yml` is untouched and unstaged.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: note the alpha separator switch in the README"
```
