# Alpha Separator Complexity Review Design

Date: 2026-10-06. Base: `main` at `f319186`, branch
`feature/optimizer-ui-rework`, review point `7f7cd73`.

Investigation:
`docs/superpowers/investigations/2026-10-06-alpha-separator-complexity-review-investigation.md`.

## 1. Overview

The branch loses about 215 lines. Eight cuts apply. Seven cuts are
behavior-preserving. One cut changes observable behavior: the preset
row shows one button instead of three, because all three shipped
presets hold identical values. Section 5 states that change and its
guard.

The cuts touch one runtime-adjacent editor surface and no analysis or
semantics code. The build classification path is untouched. The only
build-path edit is the plugin feature gate in section 6, whose
behavior the existing tests pin.

Two records from 2026-10-05 interact with this design. The
2026-10-05 complexity review plan, Task 5, rewrites the assert lines
inside `SerializedDisableControlKeepsTheBuildUntouched`. Section 8 of
this design rewrites the same lines through a shared fixture helper
and adopts the older cuts in the same edit. That older task is
superseded by this design. The 2026-10-05 title-subtext design is
unrelated and stays in force.

## 2. Inspector: label width pins inline

`SharedLabelWidthScope`, its `Begin` factory, and its
`SharedLabelWidth` helper go away, with their doc comment blocks. The
pin and the restore stay, because the restore is load-bearing:
`EditorGUIUtility.labelWidth` is process-wide static state, and a
missed restore leaks into later rows of the same GUI pass.

`DrawAlphaSeparatorSettings` replaces the scope block with:

```csharp
var widest = RowContents.Max(
    content => EditorStyles.label.CalcSize(content).x);
var previousWidth = EditorGUIUtility.labelWidth;
EditorGUIUtility.labelWidth = Mathf.Ceil(widest) + 2f;
DrawMipCapPopup();
DrawMinTextureSizePopup();
DrawCoverageSlider();
DrawAlphaPolicyControls();
EditorGUIUtility.labelWidth = previousWidth;
```

The `Ceil` and the 2 pixel padding are kept, so the drawn width is
unchanged. The restore runs on the same normal path the scope's
`Dispose` ran. A GUI exception aborts the whole `OnGUI` pass, so the
scope never protected anything beyond that path.

The widest-label measurement uses `Max`, which adds the file's
`System.Linq` import. The old helper computed the same maximum with a
`foreach` loop. The measured value is identical.

## 3. Inspector: one slider helper

`PercentSlider` absorbs the four pasted slider write-backs:

```csharp
private int PercentSlider(string propertyPath, GUIContent content)
{
    var property = serializedObject.FindProperty(propertyPath);
    property.intValue = EditorGUILayout.IntSlider(
        content,
        Mathf.Clamp(property.intValue, 0, 100),
        0, 100);
    return property.intValue;
}
```

`DrawCoverageSlider` becomes one call and keeps its method, because
the foldout body names its rows by their draw methods:

```csharp
private void DrawCoverageSlider()
{
    PercentSlider("_minimumOpaqueCoveragePercent",
        MaterialCoverageRowContent);
}
```

`DrawAlphaPolicyControls` becomes three calls and the normalize
write-back, in the same drawn order as today:

```csharp
private void DrawAlphaPolicyControls()
{
    var minimumOpaqueAlpha = PercentSlider(
        "_minimumOpaqueAlphaPercent", TextureClampRowContent);
    PercentSlider("_polygonMinimumOpaqueCoveragePercent",
        PolygonCoverageRowContent);
    var polygonClamp = PercentSlider(
        "_polygonAlphaUpperClampPercent", PolygonClampRowContent);
    var clampProperty = serializedObject.FindProperty(
        "_polygonAlphaUpperClampPercent");
    clampProperty.intValue = NormalizePolygonClamp(
        minimumOpaqueAlpha, polygonClamp);
}
```

The helper writes the drawn clamp value into the property before
`NormalizePolygonClamp` overwrites it with the normalized value. The
current code writes only the normalized value. No control draws
between the two writes, so the stored end state is identical.

The drawn order of the three sliders is unchanged: alpha percent,
polygon coverage, polygon clamp.

## 4. Drawer: BeginProperty carries the mixed value

`ToggleLeftDrawer.OnGUI` loses both manual assignments to
`EditorGUI.showMixedValue` and the doc sentence that describes them:

```csharp
label = EditorGUI.BeginProperty(position, label, property);
EditorGUI.BeginChangeCheck();
var value = EditorGUI.ToggleLeft(position, label,
    property.boolValue);
if (EditorGUI.EndChangeCheck())
{
    property.boolValue = value;
}

EditorGUI.EndProperty();
```

The Unity 2022.3 scripting reference states that `BeginProperty` and
`EndProperty` automatically handle "setting `showMixedValue` to true
if the values of the property are different when multi-object
editing". `EndProperty` restores the previous value, so the manual
reset to false is redundant, and the platform restore is more correct
than an unconditional false.

Multi-object behavior is the acceptance risk of this cut, and the
drawer has no EditMode coverage. The plan adds a manual multi-object
smoke check on the dev editor instance.

## 5. Preset data: one shipped preset

This is the one behavior change in the design. The inspector preset
row shows one button, `Safe`, instead of three. The change is honest:
`normal.json` and `aggressive.json` byte-match `safe.json` except for
name and description, and both descriptions state "The same safe
checks for now". Three buttons that press identical values are two
false promises of a choice.

The cut has four parts:

1. Delete `Presets/normal.json`, `Presets/aggressive.json`, and their
   `.meta` files.
2. Shrink `PresetFileStore.FileNames` from the three literals to
   `{ "safe" }`. A missing file fails the whole load and turns the
   preset row off, so the store list and the folder must change
   together.
3. Rewrite the store test `TheThreeShippedFilesParseInTheFixedOrder`
   to expect one shipped preset named `Safe`. The other store test,
   `EveryShippedFileHoldsTheShippedDefaults`, iterates the loaded
   presets and needs no edit.
4. Update the README preset sentence, which names all three presets.

The RED/GREEN evidence for this cut: the rewritten store test fails
first, because three files still ship. The observed failure is the
RED. The file deletion and the `FileNames` shrink then turn it green.

The editor doc comment that names the fixed order "safe, normal,
aggressive" shrinks to "One button per shipped preset file."

## 6. Plugin: the trigger returns the component

`TriggerActivated` has exactly one caller, the pipeline check at the
alpha separator barrier. It returns the component instead of a bool:

```csharp
private static Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
    ActivatedOptimizer(BuildContext context)
{
    var root = context.AvatarRootObject;
    if (root == null)
    {
        return null;
    }
    var component =
        root.GetComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
    return component != null && !component.AmuseDisabled
        ? component
        : null;
}
```

The pipeline call site becomes:

```csharp
var activated = ActivatedOptimizer(context);
if (activated == null)
{
    return;
}
```

and the barrier below it becomes:

```csharp
// Feature switch: the alpha separator runs only with its
// toggle on. The component stays present for later sibling
// features. Off mirrors the Disable AMUSE contract for this
// one feature: nothing analyzed, nothing mutated, nothing
// reported, and the consent layer below never asks.
if (!activated.AlphaSeparatorEnabled)
{
    return;
}
```

The local is named `activated` because `Execute` already holds a
local named `optimizer` deeper in its scope, at the policy reads.
Naming the new local `optimizer` is a compile error.

`AlphaSeparatorSwitchedOff` and its doc block go away. The null
guard, the root-only rule, and the silent no-op contract are
unchanged. The doc comment on the renamed method keeps the V1 trigger
wording and states the component return.

Correction, same date: the first draft of this section named the
call-site local `optimizer`. That collides with the existing
`optimizer` local at the policy reads inside the same `Execute` scope,
and the first implementation attempt failed to compile with CS0128.
The implementer's guard run surfaced it as three fixture-precondition
failures, because Unity keeps the previous assembly and refuses
behaviour creation while scripts do not compile.

The existing plugin gate tests pin both switches. Task order makes
those tests the guard: the test consolidation lands before this
refactor.

## 7. Plugin gate tests: rows for the two switches

`DisabledComponentDoesNotActivateThePipeline` and
`AlphaSeparatorSwitchedOffDoesNotActivateThePipeline` share their
whole fixture. One source drives one body:

```csharp
private static IEnumerable<TestCaseData> GateSwitchRows()
{
    yield return new TestCaseData(
        "_amuseDisabled", true, "a disabled component",
        false)
        .SetName("DisabledComponentDoesNotActivateThePipeline");
    yield return new TestCaseData(
        "_alphaSeparatorEnabled", false,
        "a switched-off alpha separator",
        true)
        .SetName("AlphaSeparatorSwitchedOffDoesNotActivateThePipeline");
}

[TestCaseSource(nameof(GateSwitchRows))]
public void SwitchedGateKeepsThePipelineIdle(
    string propertyName, bool flippedValue, string subject,
    bool expectRendererLoopStop)
{
    using var assets = new OverrideTemporaryDirectoryScope(null);
    var root = new GameObject("AMUSE gated-switch fixture");
    FixtureAvatarIdentity.AttachVrcDescriptor(root);
    var component =
        root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
    FixtureProofScope.PinAllSizes(root);
    root.AddComponent<LineRenderer>();

    try
    {
        var serialized = new UnityEditor.SerializedObject(component);
        var switchProperty = serialized.FindProperty(propertyName);
        Assert.That(switchProperty, Is.Not.Null,
            "AmuseAvatarOptimizer." + propertyName + " field pin");
        switchProperty.boolValue = flippedValue;
        serialized.ApplyModifiedProperties();

        var context = AvatarProcessor.ProcessAvatar(
            root, TestGenericPlatform.Instance);
        AmusePlatformFinishPass.Execute(context, SupportedFacts());

        var amuse = context.GetState<AmusePlatformFinishState>();
        Assert.That(amuse.AnalyzedRendererCount, Is.Zero,
            subject + " must not activate the pipeline");
        Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero,
            subject + " must not turn the loop into refusals");
        if (expectRendererLoopStop)
        {
            Assert.That(amuse.ReachedRendererAnalysis, Is.False,
                subject + " must stop before the renderer loop");
        }
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

`SetName` keeps the two behavior-sentence names, so failures and
filters still name the exact case. The unified body adds the field
pin assert to the disable row, which lacked it. That strengthens the
guard and weakens nothing. The case count stays at two.

Correction, same date: the first draft of this section dropped the
alpha row's `ReachedRendererAnalysis` assert, and the implementer's
conflict report caught it against the pre-change file. The row now
carries `expectRendererLoopStop`, and the alpha row keeps its
"must stop before the renderer loop" guard verbatim.

## 8. Integration tests: one gated-switch fixture

`SerializedDisableControlKeepsTheBuildUntouched` and
`SerializedAlphaSeparatorSwitchKeepsTheBuildUntouched` share the
setup through renderer creation. One helper replaces the shared part:

```csharp
private (GameObject root, Material transparent,
    SkinnedMeshRenderer renderer) CreateGatedSwitchFixture(
        string flipProperty,
        bool flipValue,
        string animationTag,
        string textureTag,
        string displayName)
{
    RequireIntegrationEnvironment(
        out var traceAndOptimizeType,
        out var transparentShader,
        out _);
    AssetDatabase.CreateFolder("Assets", "AmuseTests_AaoMerge");

    var root = new GameObject(displayName);
    FixtureAvatarIdentity.AttachVrcDescriptor(root);
    var amuse = root.AddComponent<AmuseAvatarOptimizer>();
    var serialized = new SerializedObject(amuse);
    var toggle = serialized.FindProperty(flipProperty);
    Assert.That(toggle, Is.Not.Null,
        "AmuseAvatarOptimizer." + flipProperty + " field pin");
    toggle.boolValue = flipValue;
    serialized.ApplyModifiedPropertiesWithoutUndo();

    AttachAnimationFixture(root, animationTag);
    var texture = Track(ImportBandedAlphaTexture(textureTag));
    var transparent = Track(NewTransparentMaterial(
        transparentShader, texture));
    var renderer = CreateSkinnedRenderer(
        root, displayName + " renderer", displayName + " mesh",
        new[] { transparent },
        new[] { Band.Transparent, Band.Partial, Band.Opaque },
        new[] { 0, 0, 0 },
        firstTriangleIndex: 0);
    return (root, transparent, renderer);
}
```

The helper is an instance method, because `AttachAnimationFixture`,
`ImportBandedAlphaTexture`, and `NewTransparentMaterial` are instance
members of the fixture class, and NUnit constructs a fresh test class
instance per test, so both callers reach it. The `transparent` local
is declared with `var`, not just named in the tuple return.

Correction, same date: the first draft of this section showed the
helper as `static` and assigned `transparent` without declaring it.
That does not compile. The defect reached the branch because a test
filter can run against the previous good assembly while a compile
error stands, which made the first guard look green. The compile
check now reads the console error log after every refresh.

Each test keeps its own name, doc comment, `try`/`finally`, the
`ProcessAvatar` call, and its assert tail, because the tails differ.
The disable tail adopts the direct asserts while the test is open:

```csharp
Assert.That(
    state.Separation?.CreatedClones,
    Is.Null.Or.Empty,
    "a disabled run must not create generated materials");
Assert.That(
    renderer.sharedMaterials,
    Has.All.Matches<Material>(material =>
        material == transparent),
    "a disabled run must leave the original material on every slot");
```

The `HashSet<Material>` wrapper and the `GetComponentsInChildren`
sweep go away. Those are the cuts the 2026-10-05 review flagged as
its findings 3.7 and 3.8, and the alpha twin already used the direct
forms. The assert message texts are kept verbatim, because they state
the contract.

The fixture object names derive from `displayName`. No assert reads
a fixture object name.

## 9. Component tests: rows for the switch fields

Four tests repeat one skeleton: `DefaultAlphaSeparatorEnabledIsTrue`,
`AlphaSeparatorEnabledSerializedPropertyCanBeToggled`,
`DefaultAdvancedSettingsRevealedIsFalse`, and
`AdvancedSettingsRevealedSerializedPropertyCanBeToggled`. Two rows
and two bodies replace them:

```csharp
private static IEnumerable<TestCaseData> SwitchFieldRows()
{
    // The reveal toggle stores on the component, so the engine
    // Reset command clears it like every other control. A missing
    // field would leave the reveal stuck on across a Reset.
    yield return new TestCaseData(
        "_advancedSettingsRevealed", false, true,
        new Func<AmuseAvatarOptimizer, bool>(o =>
            new SerializedObject(o)
                .FindProperty("_advancedSettingsRevealed")
                .boolValue));
    yield return new TestCaseData(
        "_alphaSeparatorEnabled", true, false,
        new Func<AmuseAvatarOptimizer, bool>(o =>
            o.AlphaSeparatorEnabled));
}

[TestCaseSource(nameof(SwitchFieldRows))]
public void SwitchFieldReadsItsInitializerDefault(
    string fieldName, bool defaultValue, bool toggledValue,
    Func<AmuseAvatarOptimizer, bool> read)
{
    var go = new GameObject("Root");
    try
    {
        var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
        Assert.That(read(optimizer), Is.EqualTo(defaultValue));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}

[TestCaseSource(nameof(SwitchFieldRows))]
public void SwitchFieldRoundTripsThroughTheSerializedProperty(
    string fieldName, bool defaultValue, bool toggledValue,
    Func<AmuseAvatarOptimizer, bool> read)
{
    var go = new GameObject("Root");
    try
    {
        var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
        var serializedObject = new SerializedObject(optimizer);
        var property = serializedObject.FindProperty(fieldName);
        Assert.That(property, Is.Not.Null,
            "AmuseAvatarOptimizer." + fieldName + " field pin");
        property.boolValue = toggledValue;
        serializedObject.ApplyModifiedProperties();

        Assert.That(read(optimizer), Is.EqualTo(toggledValue));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}
```

The two access modes are preserved: the alpha separator switch reads
through its public property, and the advanced settings reveal reads
through `SerializedObject`, because it has no public accessor. The
Reset comment moves onto the reveal row. The case count stays at
four: two rows through two bodies.

## 10. Test strategy

Seven cuts are behavior-preserving, so their guard is the existing
suite. The plan records the observed EditMode count for each touched
filter before and after its task, and a difference stops the task.
The row consolidations preserve case counts by construction: two gate
rows, four switch-field cases.

The preset cut is the one RED/GREEN task. The rewritten store test is
observed failing against the three-file state before the file
deletion.

The inspector cuts have no EditMode coverage. The plan adds a manual
inspector smoke check on the dev editor instance after each: rows
draw, the label column pins as the window narrows, the sliders write
back, and a multi-object selection shows the mixed-value mark on the
toggles. The Unity MCP safety preamble from the 2026-10-05 plan
applies: read-only instance check, exact `Assets` folder match, pinned
instance, and never the Census Lab project.

## 11. Documentation

Two living documents change. The README sentence names three presets,
so it changes with section 5. The editor doc comment in section 5
shrinks with the same cut. The dated records are history: the
investigation and the 2026-10-05 plan are not edited. The superseded
status of the 2026-10-05 plan Task 5 is stated in this design and in
the new plan, and each record still speaks for its own date.
