# Deterministic Reference Fixtures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add thirteen public, deterministic texture/mesh reference fixtures and executable integrity specifications. Do not add an alpha classifier or production behavior.

**Architecture:** Store portable inputs and independent triangle-outcome oracles in two package-local JSON catalogs. Parse, validate, and build fresh disposable Unity objects entirely in the existing EditMode test assembly. Tests validate the fixture framework and fixed oracle contract. They never derive outcomes through Unity sampling or classifier logic.

**Tech Stack:** Unity 2022.3.22f1, C# Editor tests, NUnit, `UnityEngine.JsonUtility`, `UnityEditor.AssetDatabase`, `Texture2D`, and `Mesh`.

## Global Constraints

- Keep every new implementation file under `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/`.
- Add exactly thirteen approved cases. Preserve the IDs and outcomes from `docs/superpowers/specs/2026-08-15-reference-fixtures-design.md`.
- Keep `fixture-inputs.json` independent from `fixture-expectations.json`. Do not calculate expected outcomes from Unity sampling or test-side classifier logic.
- Alpha byte `255` is the only fully opaque value. When partial transparency is known, every value below `255` is `MustRemainTransparent`, including `254`.
- `Unknown` means “the analyzer cannot establish a safe supported classification from the available information.”
- Interpret every nondegenerate triangle over its continuous closed barycentric UV domain. Include the interior, edges, vertices, and filter footprint.
- Treat structural numeric array order as significant. Treat texture, mesh, case, and expectation record collection order as insignificant.
- Build fresh case-local `Texture2D` and `Mesh` objects. Disable mipmaps and prevent state leakage between cases.
- Use only the existing Unity and NUnit dependencies. Do not add packages.
- Do not add a production classifier, transformation, NDMF, material, animation, shader-adapter, or private-avatar code.
- Do not add mipmap cases, CI, or release/listing workflow changes.
- Do not access or modify the private Unity testbed.

---

## File map

**Create:**

- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-inputs.json` — portable alpha, geometry, UV, and sampling inputs.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-expectations.json` — independent per-triangle semantic oracle.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs` — JSON DTOs, catalog loading/validation, ID lookup, and test-only Unity builders.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureIntegrityTests.cs` — framework, oracle-integrity, determinism, isolation, and mipmap tests.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures.meta` — Unity folder identity.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data.meta` — Unity data-folder identity.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-inputs.json.meta` — input-catalog identity.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-expectations.json.meta` — expectation-catalog identity.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs.meta` — support-code identity.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureIntegrityTests.cs.meta` — test-code identity.

**Do not modify:**

- `Packages/com.alrauna.alpha-material-optimizer/Editor/`
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Alrauna.AlphaMaterialOptimizer.Tests.Editor.asmdef`
- package manifests or locks
- `.github/workflows/`

## Shared interfaces

The following block describes the final test-assembly interfaces in `ReferenceFixtureData.cs`. Task 1 adds the catalog types and methods through `FindExpectation`. Task 2 adds `BuiltReferenceFixture` and `BuildCase`. Use the exact names throughout:

```csharp
[Serializable]
internal sealed class FixtureInputCatalog
{
    public int schemaVersion;
    public TextureFixtureRecord[] textures;
    public MeshFixtureRecord[] meshes;
    public FixtureCaseRecord[] cases;
}

[Serializable]
internal sealed class TextureFixtureRecord
{
    public string id;
    public int width;
    public int height;
    public int[] alpha8BottomToTop;
}

[Serializable]
internal sealed class MeshFixtureRecord
{
    public string id;
    public float[] positions;
    public string uv0Status;
    public float[] uv0;
    public int[] triangleVertexIndices;
}

[Serializable]
internal sealed class FixtureCaseRecord
{
    public string id;
    public string textureId;
    public string meshId;
    public string filterMode;
    public string wrapMode;
}

[Serializable]
internal sealed class FixtureExpectationCatalog
{
    public int schemaVersion;
    public FixtureExpectationRecord[] cases;
}

[Serializable]
internal sealed class FixtureExpectationRecord
{
    public string caseId;
    public TriangleOutcomeRecord[] triangleOutcomes;
}

[Serializable]
internal sealed class TriangleOutcomeRecord
{
    public int triangleIndex;
    public string outcome;
}

internal sealed class ReferenceFixtureCatalogs
{
    internal FixtureInputCatalog Inputs { get; }
    internal FixtureExpectationCatalog Expectations { get; }

    internal ReferenceFixtureCatalogs(
        FixtureInputCatalog inputs,
        FixtureExpectationCatalog expectations)
    {
        Inputs = inputs;
        Expectations = expectations;
    }
}

internal sealed class BuiltReferenceFixture : IDisposable
{
    internal Texture2D Texture { get; }
    internal Mesh Mesh { get; }

    internal BuiltReferenceFixture(Texture2D texture, Mesh mesh)
    {
        Texture = texture;
        Mesh = mesh;
    }

    public void Dispose()
    {
        UnityEngine.Object.DestroyImmediate(Texture);
        UnityEngine.Object.DestroyImmediate(Mesh);
    }
}

internal static class ReferenceFixtureData
{
    internal const string InputsPath =
        "Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-inputs.json";
    internal const string ExpectationsPath =
        "Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-expectations.json";

    internal static ReferenceFixtureCatalogs Load();
    internal static void Validate(ReferenceFixtureCatalogs catalogs);
    internal static FixtureCaseRecord FindCase(FixtureInputCatalog inputs, string caseId);
    internal static FixtureExpectationRecord FindExpectation(
        FixtureExpectationCatalog expectations,
        string caseId);
    internal static BuiltReferenceFixture BuildCase(FixtureInputCatalog inputs, string caseId);
}
```

---

### Task 1: Portable catalogs and structural/oracle validation

**Files:**

- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-inputs.json`
- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-expectations.json`
- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs`
- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureIntegrityTests.cs`
- Create matching `.meta` files through Unity import

**Interfaces:**

- Consumes: the approved schema and semantic outcomes in `docs/superpowers/specs/2026-08-15-reference-fixtures-design.md`.
- Produces: `ReferenceFixtureData.Load`, `Validate`, `FindCase`, and `FindExpectation`, plus the DTO types listed under Shared interfaces.

- [ ] **Step 1: Add failing catalog-contract tests**

Create `ReferenceFixtureIntegrityTests.cs` with these tests before you add `ReferenceFixtureData.cs` or either JSON catalog:

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.AlphaMaterialOptimizer.Tests.Editor.ReferenceFixtures
{
    public sealed class ReferenceFixtureIntegrityTests
    {
        private static readonly string[] ExpectedCaseIds =
        {
            "fully-opaque-texture",
            "alpha-254-boundary",
            "fully-transparent-texture",
            "mixed-alpha-texture",
            "triangle-in-opaque-region",
            "triangle-in-transparent-region",
            "triangle-crosses-alpha-boundary",
            "mixed-triangle-mesh",
            "outside-uv-clamp",
            "outside-uv-repeat",
            "degenerate-triangle",
            "missing-uv0",
            "bilinear-filter-boundary"
        };

        [Test]
        public void CatalogsLoadDeterministicallyAndValidate()
        {
            var first = ReferenceFixtureData.Load();
            var second = ReferenceFixtureData.Load();

            Assert.That(JsonUtility.ToJson(first.Inputs),
                Is.EqualTo(JsonUtility.ToJson(second.Inputs)));
            Assert.That(JsonUtility.ToJson(first.Expectations),
                Is.EqualTo(JsonUtility.ToJson(second.Expectations)));
            Assert.DoesNotThrow(() => ReferenceFixtureData.Validate(first));
        }

        [Test]
        public void CatalogContainsExactlyApprovedCaseIds()
        {
            var catalogs = ReferenceFixtureData.Load();
            CollectionAssert.AreEquivalent(
                ExpectedCaseIds,
                catalogs.Inputs.cases.Select(item => item.id));
            CollectionAssert.AreEquivalent(
                ExpectedCaseIds,
                catalogs.Expectations.cases.Select(item => item.caseId));
        }

        [Test]
        public void RecordCollectionOrderDoesNotAffectResolution()
        {
            var catalogs = ReferenceFixtureData.Load();
            Array.Reverse(catalogs.Inputs.textures);
            Array.Reverse(catalogs.Inputs.meshes);
            Array.Reverse(catalogs.Inputs.cases);
            Array.Reverse(catalogs.Expectations.cases);

            Assert.DoesNotThrow(() => ReferenceFixtureData.Validate(catalogs));
            Assert.That(
                ReferenceFixtureData.FindCase(catalogs.Inputs, "outside-uv-repeat").wrapMode,
                Is.EqualTo("Repeat"));
            Assert.That(
                ReferenceFixtureData.FindExpectation(
                    catalogs.Expectations,
                    "outside-uv-repeat").triangleOutcomes[0].outcome,
                Is.EqualTo("ProvenOpaque"));
        }

        [TestCase("alpha-254-boundary", 0, "MustRemainTransparent")]
        [TestCase("degenerate-triangle", 0, "Unknown")]
        [TestCase("missing-uv0", 0, "Unknown")]
        public void ConservativeBoundaryOutcomesAreExplicit(
            string caseId,
            int triangleIndex,
            string expectedOutcome)
        {
            var expectation = ReferenceFixtureData.FindExpectation(
                ReferenceFixtureData.Load().Expectations,
                caseId);
            var triangle = expectation.triangleOutcomes.Single(
                item => item.triangleIndex == triangleIndex);

            Assert.That(triangle.outcome, Is.EqualTo(expectedOutcome));
        }
    }
}
```

- [ ] **Step 2: Run the focused EditMode test and observe the expected failure**

Run from the repository root:

```powershell
$unityExe = '<unity-editor-path>\2022.3.22f1\Editor\Unity.exe'
$projectRoot = '<repo-root>'
& $unityExe -batchmode -nographics -projectPath $projectRoot -runTests -testPlatform EditMode -testFilter 'Alrauna.AlphaMaterialOptimizer.Tests.Editor.ReferenceFixtures.ReferenceFixtureIntegrityTests' -testResults "$projectRoot\Logs\reference-fixtures-red.xml" -logFile "$projectRoot\Logs\reference-fixtures-red.log"
```

Expected: a nonzero exit or compiler failure in `reference-fixtures-red.log` because `ReferenceFixtureData` does not exist. Unity can generate `.meta` files during this import. Retain only metas paired with the intended new fixture files and directories.

- [ ] **Step 3: Add the exact portable input catalog**

Create `fixture-inputs.json` with schema version 1, these five textures, these nine meshes, and these thirteen cases:

```json
{
  "schemaVersion": 1,
  "textures": [
    { "id": "alpha-255-2x2", "width": 2, "height": 2, "alpha8BottomToTop": [255, 255, 255, 255] },
    { "id": "alpha-254-2x2", "width": 2, "height": 2, "alpha8BottomToTop": [254, 254, 254, 254] },
    { "id": "alpha-0-2x2", "width": 2, "height": 2, "alpha8BottomToTop": [0, 0, 0, 0] },
    { "id": "mixed-checker-2x2", "width": 2, "height": 2, "alpha8BottomToTop": [255, 0, 0, 255] },
    { "id": "split-vertical-4x4", "width": 4, "height": 4, "alpha8BottomToTop": [255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0] }
  ],
  "meshes": [
    { "id": "single-triangle-unit", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "uv0Status": "Present", "uv0": [0, 0, 1, 0, 0, 1], "triangleVertexIndices": [0, 1, 2] },
    { "id": "triangle-opaque-region", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "uv0Status": "Present", "uv0": [0.125, 0.125, 0.375, 0.125, 0.125, 0.375], "triangleVertexIndices": [0, 1, 2] },
    { "id": "triangle-transparent-region", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "uv0Status": "Present", "uv0": [0.625, 0.125, 0.875, 0.125, 0.625, 0.375], "triangleVertexIndices": [0, 1, 2] },
    { "id": "triangle-cross-boundary", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "uv0Status": "Present", "uv0": [0.25, 0.25, 0.75, 0.25, 0.25, 0.75], "triangleVertexIndices": [0, 1, 2] },
    { "id": "two-triangles-mixed", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0, 2, 0, 0, 3, 0, 0, 2, 1, 0], "uv0Status": "Present", "uv0": [0.125, 0.125, 0.375, 0.125, 0.125, 0.375, 0.625, 0.125, 0.875, 0.125, 0.625, 0.375], "triangleVertexIndices": [0, 1, 2, 3, 4, 5] },
    { "id": "triangle-outside-u", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "uv0Status": "Present", "uv0": [1.125, 0.125, 1.375, 0.125, 1.125, 0.375], "triangleVertexIndices": [0, 1, 2] },
    { "id": "triangle-degenerate", "positions": [0, 0, 0, 1, 0, 0, 2, 0, 0], "uv0Status": "Present", "uv0": [0.125, 0.125, 0.375, 0.125, 0.125, 0.375], "triangleVertexIndices": [0, 1, 2] },
    { "id": "triangle-missing-uv0", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "uv0Status": "Missing", "uv0": [], "triangleVertexIndices": [0, 1, 2] },
    { "id": "triangle-bilinear-boundary", "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "uv0Status": "Present", "uv0": [0.375, 0.25, 0.49, 0.25, 0.375, 0.75], "triangleVertexIndices": [0, 1, 2] }
  ],
  "cases": [
    { "id": "fully-opaque-texture", "textureId": "alpha-255-2x2", "meshId": "single-triangle-unit", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "alpha-254-boundary", "textureId": "alpha-254-2x2", "meshId": "single-triangle-unit", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "fully-transparent-texture", "textureId": "alpha-0-2x2", "meshId": "single-triangle-unit", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "mixed-alpha-texture", "textureId": "mixed-checker-2x2", "meshId": "single-triangle-unit", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "triangle-in-opaque-region", "textureId": "split-vertical-4x4", "meshId": "triangle-opaque-region", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "triangle-in-transparent-region", "textureId": "split-vertical-4x4", "meshId": "triangle-transparent-region", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "triangle-crosses-alpha-boundary", "textureId": "split-vertical-4x4", "meshId": "triangle-cross-boundary", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "mixed-triangle-mesh", "textureId": "split-vertical-4x4", "meshId": "two-triangles-mixed", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "outside-uv-clamp", "textureId": "split-vertical-4x4", "meshId": "triangle-outside-u", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "outside-uv-repeat", "textureId": "split-vertical-4x4", "meshId": "triangle-outside-u", "filterMode": "Point", "wrapMode": "Repeat" },
    { "id": "degenerate-triangle", "textureId": "alpha-255-2x2", "meshId": "triangle-degenerate", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "missing-uv0", "textureId": "alpha-255-2x2", "meshId": "triangle-missing-uv0", "filterMode": "Point", "wrapMode": "Clamp" },
    { "id": "bilinear-filter-boundary", "textureId": "split-vertical-4x4", "meshId": "triangle-bilinear-boundary", "filterMode": "Bilinear", "wrapMode": "Clamp" }
  ]
}
```

- [ ] **Step 4: Add the independent semantic oracle**

Create `fixture-expectations.json` directly from the approved specification. Do not read or sample the input catalog to generate it:

```json
{
  "schemaVersion": 1,
  "cases": [
    { "caseId": "fully-opaque-texture", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "ProvenOpaque" }] },
    { "caseId": "alpha-254-boundary", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "MustRemainTransparent" }] },
    { "caseId": "fully-transparent-texture", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "MustRemainTransparent" }] },
    { "caseId": "mixed-alpha-texture", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "MustRemainTransparent" }] },
    { "caseId": "triangle-in-opaque-region", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "ProvenOpaque" }] },
    { "caseId": "triangle-in-transparent-region", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "MustRemainTransparent" }] },
    { "caseId": "triangle-crosses-alpha-boundary", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "MustRemainTransparent" }] },
    { "caseId": "mixed-triangle-mesh", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "ProvenOpaque" }, { "triangleIndex": 1, "outcome": "MustRemainTransparent" }] },
    { "caseId": "outside-uv-clamp", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "MustRemainTransparent" }] },
    { "caseId": "outside-uv-repeat", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "ProvenOpaque" }] },
    { "caseId": "degenerate-triangle", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "Unknown" }] },
    { "caseId": "missing-uv0", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "Unknown" }] },
    { "caseId": "bilinear-filter-boundary", "triangleOutcomes": [{ "triangleIndex": 0, "outcome": "MustRemainTransparent" }] }
  ]
}
```

- [ ] **Step 5: Implement parsing, lookup, and exact structural validation**

Create `ReferenceFixtureData.cs` with these task-local DTO and catalog definitions:

```csharp
[Serializable]
internal sealed class FixtureInputCatalog
{
    public int schemaVersion;
    public TextureFixtureRecord[] textures;
    public MeshFixtureRecord[] meshes;
    public FixtureCaseRecord[] cases;
}

[Serializable]
internal sealed class TextureFixtureRecord
{
    public string id;
    public int width;
    public int height;
    public int[] alpha8BottomToTop;
}

[Serializable]
internal sealed class MeshFixtureRecord
{
    public string id;
    public float[] positions;
    public string uv0Status;
    public float[] uv0;
    public int[] triangleVertexIndices;
}

[Serializable]
internal sealed class FixtureCaseRecord
{
    public string id;
    public string textureId;
    public string meshId;
    public string filterMode;
    public string wrapMode;
}

[Serializable]
internal sealed class FixtureExpectationCatalog
{
    public int schemaVersion;
    public FixtureExpectationRecord[] cases;
}

[Serializable]
internal sealed class FixtureExpectationRecord
{
    public string caseId;
    public TriangleOutcomeRecord[] triangleOutcomes;
}

[Serializable]
internal sealed class TriangleOutcomeRecord
{
    public int triangleIndex;
    public string outcome;
}

internal sealed class ReferenceFixtureCatalogs
{
    internal FixtureInputCatalog Inputs { get; }
    internal FixtureExpectationCatalog Expectations { get; }

    internal ReferenceFixtureCatalogs(
        FixtureInputCatalog inputs,
        FixtureExpectationCatalog expectations)
    {
        Inputs = inputs;
        Expectations = expectations;
    }
}
```

`Load` must load each file as a `TextAsset` and parse it with `JsonUtility.FromJson`. It must construct `ReferenceFixtureCatalogs`, call `Validate`, and return the validated catalogs:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

private const int SupportedSchemaVersion = 1;
private static readonly HashSet<string> FilterModes =
    new HashSet<string>(new[] { "Point", "Bilinear" }, StringComparer.Ordinal);
private static readonly HashSet<string> WrapModes =
    new HashSet<string>(new[] { "Clamp", "Repeat" }, StringComparer.Ordinal);
private static readonly HashSet<string> UvStates =
    new HashSet<string>(new[] { "Present", "Missing" }, StringComparer.Ordinal);
private static readonly HashSet<string> Outcomes =
    new HashSet<string>(new[] { "ProvenOpaque", "MustRemainTransparent", "Unknown" }, StringComparer.Ordinal);

internal static ReferenceFixtureCatalogs Load()
{
    var inputsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(InputsPath);
    var expectationsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ExpectationsPath);
    Require(inputsAsset != null, $"Fixture input catalog does not exist: {InputsPath}");
    Require(expectationsAsset != null, $"Fixture expectation catalog does not exist: {ExpectationsPath}");

    var catalogs = new ReferenceFixtureCatalogs(
        JsonUtility.FromJson<FixtureInputCatalog>(inputsAsset.text),
        JsonUtility.FromJson<FixtureExpectationCatalog>(expectationsAsset.text));
    Validate(catalogs);
    return catalogs;
}

private static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidDataException(message);
    }
}
```

Implement `Validate` with all the following concrete checks. When an offending ID exists, include it in the failure message:

1. Both catalogs and every top-level array are non-null. Both schema versions equal `1`.
2. One ordinal `HashSet<string>` receives every texture, mesh, and input-case ID. Reject empty IDs and duplicates across those three collections.
3. Every texture has positive dimensions and exactly `width * height` alpha values. All values are in `[0, 255]`.
4. Every mesh has a non-null position array with at least nine values and a length divisible by three. Every position component is finite. Every `z` component equals zero.
5. `Present` UV0 has exactly two finite values per vertex. `Missing` UV0 has a non-null empty array. Reject every other UV state.
6. Every triangle-index array is non-null, non-empty, and divisible by three. It contains only indices from zero through `vertexCount - 1`.
7. For every indexed triangle, calculate the signed XY double area `(p1.x - p0.x) * (p2.y - p0.y) - (p1.y - p0.y) * (p2.x - p0.x)`. Reject negative values. Positive values are counter-clockwise. Allow zero for the deliberate degenerate fixture.
8. Every case references an existing texture and mesh ID. Every case uses a closed-set filter and wrap string.
9. Expectation case IDs are non-empty and unique. They correspond one-to-one with input case IDs and do not depend on collection position.
10. Each expected triangle index is unique within its case and lies in `[0, triangleCount - 1]`. The set covers every triangle exactly once.
11. Every outcome belongs to the closed outcome set. Reject unknown strings instead of defaulting to `ProvenOpaque`.

Implement `FindCase` and `FindExpectation` with ordinal ID comparison. Use an `InvalidDataException` when no unique match exists. Do not index or join records by array position.

Use one private lookup helper. This helper must apply the same uniqueness behavior everywhere:

```csharp
private static T FindUnique<T>(
    T[] records,
    string id,
    Func<T, string> selectId,
    string kind)
    where T : class
{
    T match = null;
    foreach (var record in records)
    {
        if (!string.Equals(selectId(record), id, StringComparison.Ordinal))
        {
            continue;
        }

        Require(match == null, $"Duplicate {kind} ID: {id}");
        match = record;
    }

    Require(match != null, $"Unknown {kind} ID: {id}");
    return match;
}

internal static FixtureCaseRecord FindCase(FixtureInputCatalog inputs, string caseId)
{
    return FindUnique(inputs.cases, caseId, item => item.id, "case");
}

internal static FixtureExpectationRecord FindExpectation(
    FixtureExpectationCatalog expectations,
    string caseId)
{
    return FindUnique(expectations.cases, caseId, item => item.caseId, "expectation case");
}
```

- [ ] **Step 6: Run the focused tests and confirm they pass**

Run:

```powershell
$unityExe = '<unity-editor-path>\2022.3.22f1\Editor\Unity.exe'
$projectRoot = '<repo-root>'
& $unityExe -batchmode -nographics -projectPath $projectRoot -runTests -testPlatform EditMode -testFilter 'Alrauna.AlphaMaterialOptimizer.Tests.Editor.ReferenceFixtures.ReferenceFixtureIntegrityTests' -testResults "$projectRoot\Logs\reference-fixtures-task1.xml" -logFile "$projectRoot\Logs\reference-fixtures-task1.log"
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath "$projectRoot\Logs\reference-fixtures-task1.log" -Tail 200; exit $LASTEXITCODE }
[xml]$results = Get-Content -Raw -LiteralPath "$projectRoot\Logs\reference-fixtures-task1.xml"
if ([int]$results.'test-run'.failed -ne 0) { throw 'Reference fixture integrity tests failed.' }
```

Expected: exit code `0`, zero failures in the result XML, and a paired `.meta` file for every intended Unity asset.

- [ ] **Step 7: Review asset and Git scope**

Run:

```powershell
git status --short --untracked-files=all
git diff --check
git diff -- Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures
```

Expected: only the two catalogs, two C# files, and their directory/file `.meta` partners appear. No package manifests, locks, production files, workflows, or ignored Unity state appear.

- [ ] **Step 8: Commit the validated catalogs and loader**

```powershell
git add -- Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures
git diff --cached --check
git commit -m "test: add deterministic reference fixture catalogs"
```

---

### Task 2: Fresh Unity builders, isolation, and complete verification

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureIntegrityTests.cs`

**Interfaces:**

- Consumes: validated `FixtureInputCatalog` plus a case ID through `BuildCase(FixtureInputCatalog inputs, string caseId)`.
- Produces: a fresh disposable `BuiltReferenceFixture` containing one case-local `Texture2D` and `Mesh` with no mipmaps.

- [ ] **Step 1: Add failing builder determinism, isolation, and mipmap tests**

Append these tests to `ReferenceFixtureIntegrityTests` before you add `BuiltReferenceFixture` or `BuildCase`:

```csharp
[Test]
public void EveryCaseBuildsDeterministicallyWithoutMipmaps()
{
    var inputs = ReferenceFixtureData.Load().Inputs;

    foreach (var fixtureCase in inputs.cases)
    {
        var textureRecord = inputs.textures.Single(item => item.id == fixtureCase.textureId);
        var meshRecord = inputs.meshes.Single(item => item.id == fixtureCase.meshId);

        using (var first = ReferenceFixtureData.BuildCase(inputs, fixtureCase.id))
        using (var second = ReferenceFixtureData.BuildCase(inputs, fixtureCase.id))
        {
            Assert.That(first.Texture, Is.Not.SameAs(second.Texture), fixtureCase.id);
            Assert.That(first.Mesh, Is.Not.SameAs(second.Mesh), fixtureCase.id);
            Assert.That(first.Texture.mipmapCount, Is.EqualTo(1), fixtureCase.id);
            Assert.That(first.Texture.width, Is.EqualTo(textureRecord.width), fixtureCase.id);
            Assert.That(first.Texture.height, Is.EqualTo(textureRecord.height), fixtureCase.id);
            CollectionAssert.AreEqual(
                textureRecord.alpha8BottomToTop.Select(alpha => (byte)alpha),
                first.Texture.GetPixels32().Select(pixel => pixel.a),
                fixtureCase.id);
            CollectionAssert.AreEqual(
                meshRecord.positions,
                first.Mesh.vertices.SelectMany(vertex => new[] { vertex.x, vertex.y, vertex.z }),
                fixtureCase.id);
            CollectionAssert.AreEqual(meshRecord.uv0, first.Mesh.uv.SelectMany(uv => new[] { uv.x, uv.y }), fixtureCase.id);
            CollectionAssert.AreEqual(meshRecord.triangleVertexIndices, first.Mesh.triangles, fixtureCase.id);
            Assert.That(
                first.Texture.filterMode,
                Is.EqualTo(fixtureCase.filterMode == "Point" ? FilterMode.Point : FilterMode.Bilinear),
                fixtureCase.id);
            Assert.That(
                first.Texture.wrapMode,
                Is.EqualTo(fixtureCase.wrapMode == "Clamp" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat),
                fixtureCase.id);
            CollectionAssert.AreEqual(first.Texture.GetPixels32(), second.Texture.GetPixels32(), fixtureCase.id);
            CollectionAssert.AreEqual(first.Mesh.vertices, second.Mesh.vertices, fixtureCase.id);
            CollectionAssert.AreEqual(first.Mesh.uv, second.Mesh.uv, fixtureCase.id);
            CollectionAssert.AreEqual(first.Mesh.triangles, second.Mesh.triangles, fixtureCase.id);
            Assert.That(first.Texture.filterMode, Is.EqualTo(second.Texture.filterMode), fixtureCase.id);
            Assert.That(first.Texture.wrapMode, Is.EqualTo(second.Texture.wrapMode), fixtureCase.id);
        }
    }
}

[Test]
public void SharedLogicalDefinitionsDoNotShareMutableUnityObjects()
{
    var inputs = ReferenceFixtureData.Load().Inputs;

    using (var clamp = ReferenceFixtureData.BuildCase(inputs, "outside-uv-clamp"))
    using (var repeat = ReferenceFixtureData.BuildCase(inputs, "outside-uv-repeat"))
    {
        Assert.That(clamp.Texture, Is.Not.SameAs(repeat.Texture));
        Assert.That(clamp.Mesh, Is.Not.SameAs(repeat.Mesh));
        Assert.That(clamp.Texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        Assert.That(repeat.Texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));

        repeat.Texture.filterMode = FilterMode.Trilinear;
        Assert.That(clamp.Texture.filterMode, Is.EqualTo(FilterMode.Point));
    }
}
```

These calls inspect only stored pixel data. They do not use `GetPixel`, `GetPixelBilinear`, rendering, or any Unity sampling API to infer semantic outcomes.

- [ ] **Step 2: Run the focused test and observe the expected failure**

Use the Task 1 focused Unity command with result names `reference-fixtures-builder-red.xml` and `reference-fixtures-builder-red.log`.

Expected: compiler failure because `BuiltReferenceFixture` and `ReferenceFixtureData.BuildCase` do not exist.

- [ ] **Step 3: Implement fresh case-local Unity construction**

Add the case-local disposable holder:

```csharp
internal sealed class BuiltReferenceFixture : IDisposable
{
    internal Texture2D Texture { get; }
    internal Mesh Mesh { get; }

    internal BuiltReferenceFixture(Texture2D texture, Mesh mesh)
    {
        Texture = texture;
        Mesh = mesh;
    }

    public void Dispose()
    {
        UnityEngine.Object.DestroyImmediate(Texture);
        UnityEngine.Object.DestroyImmediate(Mesh);
    }
}
```

Implement `BuildCase` with this flow:

```csharp
internal static BuiltReferenceFixture BuildCase(FixtureInputCatalog inputs, string caseId)
{
    var fixtureCase = FindCase(inputs, caseId);
    var textureRecord = FindUnique(inputs.textures, fixtureCase.textureId, item => item.id, "texture");
    var meshRecord = FindUnique(inputs.meshes, fixtureCase.meshId, item => item.id, "mesh");

    var texture = new Texture2D(
        textureRecord.width,
        textureRecord.height,
        TextureFormat.RGBA32,
        mipChain: false,
        linear: true)
    {
        name = fixtureCase.id + "-texture",
        filterMode = ParseFilterMode(fixtureCase.filterMode),
        wrapMode = ParseWrapMode(fixtureCase.wrapMode)
    };

    var pixels = textureRecord.alpha8BottomToTop
        .Select(alpha => new Color32(255, 255, 255, (byte)alpha))
        .ToArray();
    texture.SetPixels32(pixels);
    texture.Apply(false, false);

    var mesh = new Mesh { name = fixtureCase.id + "-mesh" };
    mesh.vertices = ToVector3Array(meshRecord.positions);
    if (string.Equals(meshRecord.uv0Status, "Present", StringComparison.Ordinal))
    {
        mesh.uv = ToVector2Array(meshRecord.uv0);
    }
    mesh.triangles = (int[])meshRecord.triangleVertexIndices.Clone();

    return new BuiltReferenceFixture(texture, mesh);
}
```

Implement the small conversion and closed-string parsers directly in `ReferenceFixtureData`. Do not add interfaces, factories, caches, or new files:

```csharp
private static Vector3[] ToVector3Array(float[] values)
{
    var result = new Vector3[values.Length / 3];
    for (var index = 0; index < result.Length; index++)
    {
        result[index] = new Vector3(
            values[index * 3],
            values[index * 3 + 1],
            values[index * 3 + 2]);
    }
    return result;
}

private static Vector2[] ToVector2Array(float[] values)
{
    var result = new Vector2[values.Length / 2];
    for (var index = 0; index < result.Length; index++)
    {
        result[index] = new Vector2(values[index * 2], values[index * 2 + 1]);
    }
    return result;
}

private static FilterMode ParseFilterMode(string value)
{
    if (value == "Point") return FilterMode.Point;
    if (value == "Bilinear") return FilterMode.Bilinear;
    throw new InvalidDataException($"Unsupported filter mode: {value}");
}

private static TextureWrapMode ParseWrapMode(string value)
{
    if (value == "Clamp") return TextureWrapMode.Clamp;
    if (value == "Repeat") return TextureWrapMode.Repeat;
    throw new InvalidDataException($"Unsupported wrap mode: {value}");
}
```

`Validate` has already rejected every other string before builders run. The parsers still fail closed when called independently. Do not cache the logical lookup result as a mutable Unity object. Do not cache the constructed Unity objects. The `Texture2D` constructor explicitly sets `mipChain: false`. `Apply(false, false)` must not generate mipmaps.

- [ ] **Step 4: Run focused fixture tests and inspect the observed result**

Run the Task 1 focused Unity command with result names `reference-fixtures-task2.xml` and `reference-fixtures-task2.log`.

Expected: exit code `0`, all `ReferenceFixtureIntegrityTests` pass, and the log contains zero compiler errors.

- [ ] **Step 5: Run the complete EditMode suite**

```powershell
$unityExe = '<unity-editor-path>\2022.3.22f1\Editor\Unity.exe'
$projectRoot = '<repo-root>'
& $unityExe -batchmode -nographics -projectPath $projectRoot -runTests -testPlatform EditMode -testResults "$projectRoot\Logs\editmode-results.xml" -logFile "$projectRoot\Logs\editmode.log"
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath "$projectRoot\Logs\editmode.log" -Tail 200; exit $LASTEXITCODE }
[xml]$results = Get-Content -Raw -LiteralPath "$projectRoot\Logs\editmode-results.xml"
if ([int]$results.'test-run'.failed -ne 0) { throw 'Complete EditMode suite failed.' }
if (Select-String -LiteralPath "$projectRoot\Logs\editmode.log" -Pattern 'error CS\d+|Compilation failed') { throw 'Compiler error found in Unity log.' }
```

Expected: the existing smoke test and every new fixture integrity test pass. The result XML reports zero failures. The Unity log contains zero compiler errors.

- [ ] **Step 6: Verify repository and Unity asset integrity**

```powershell
git status --short --untracked-files=all
git diff --check
git diff --cached --check
git diff -- Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures
git diff -- Packages/manifest.json Packages/packages-lock.json Packages/vpm-manifest.json .github/workflows
```

Expected:

- Only intended fixture JSON, C#, and paired `.meta` files differ from the Task 1 commit.
- No manifest, lock, workflow, production, private, or generated Unity files differ.
- Every asset has exactly one stable `.meta` partner.
- There is no classifier, transformation, NDMF pass, material rewriting, animation tracing, shader adapter, mipmap case, or private-avatar logic.

- [ ] **Step 7: Commit the verified builder and integrity tests**

```powershell
git add -- Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureIntegrityTests.cs
git diff --cached --check
git diff --cached --stat
git commit -m "test: validate deterministic Unity fixture construction"
```

- [ ] **Step 8: Record final evidence**

Run `git status --short --branch` and `git log -4 --oneline --decorate`. Report the exact EditMode test count and zero-failure result from `Logs/editmode-results.xml`. Report whether any validation was skipped. Report remaining unsupported cases, such as mipmaps. Report that the private Unity testbed was neither used nor modified.
