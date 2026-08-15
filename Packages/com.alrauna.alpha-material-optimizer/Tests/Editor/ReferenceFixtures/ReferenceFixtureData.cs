using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Alrauna.AlphaMaterialOptimizer.Tests.Editor.ReferenceFixtures
{
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

        internal static void Validate(ReferenceFixtureCatalogs catalogs)
        {
            Require(catalogs != null, "Fixture catalogs are missing.");
            Require(catalogs.Inputs != null, "Fixture input catalog is missing.");
            Require(catalogs.Expectations != null, "Fixture expectation catalog is missing.");

            var inputs = catalogs.Inputs;
            var expectations = catalogs.Expectations;
            Require(inputs.textures != null, "Fixture texture records are missing.");
            Require(inputs.meshes != null, "Fixture mesh records are missing.");
            Require(inputs.cases != null, "Fixture input case records are missing.");
            Require(expectations.cases != null, "Fixture expectation case records are missing.");
            Require(inputs.schemaVersion == SupportedSchemaVersion,
                $"Unsupported input schema version: {inputs.schemaVersion}");
            Require(expectations.schemaVersion == SupportedSchemaVersion,
                $"Unsupported expectation schema version: {expectations.schemaVersion}");

            var recordIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var texture in inputs.textures)
            {
                Require(texture != null, "Fixture texture record is missing.");
                AddRecordId(recordIds, texture.id, "texture");
                ValidateTexture(texture);
            }

            foreach (var mesh in inputs.meshes)
            {
                Require(mesh != null, "Fixture mesh record is missing.");
                AddRecordId(recordIds, mesh.id, "mesh");
                ValidateMesh(mesh);
            }

            foreach (var fixtureCase in inputs.cases)
            {
                Require(fixtureCase != null, "Fixture input case record is missing.");
                AddRecordId(recordIds, fixtureCase.id, "case");
                FindUnique(inputs.textures, fixtureCase.textureId, item => item.id, "texture");
                FindUnique(inputs.meshes, fixtureCase.meshId, item => item.id, "mesh");
                Require(FilterModes.Contains(fixtureCase.filterMode),
                    $"Invalid filter mode for case {fixtureCase.id}: {fixtureCase.filterMode}");
                Require(WrapModes.Contains(fixtureCase.wrapMode),
                    $"Invalid wrap mode for case {fixtureCase.id}: {fixtureCase.wrapMode}");
            }

            var expectationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var expectation in expectations.cases)
            {
                Require(expectation != null, "Fixture expectation case record is missing.");
                Require(!string.IsNullOrEmpty(expectation.caseId), "Expectation case ID is empty.");
                Require(expectationIds.Add(expectation.caseId),
                    $"Duplicate expectation case ID: {expectation.caseId}");

                var fixtureCase = FindCase(inputs, expectation.caseId);
                var mesh = FindUnique(inputs.meshes, fixtureCase.meshId, item => item.id, "mesh");
                ValidateOutcomes(expectation, mesh.triangleVertexIndices.Length / 3);
            }

            Require(expectationIds.Count == inputs.cases.Length,
                "Expectation cases do not correspond one-to-one with input cases.");
        }

        internal static FixtureCaseRecord FindCase(FixtureInputCatalog inputs, string caseId)
        {
            Require(inputs != null, "Fixture input catalog is missing.");
            return FindUnique(inputs.cases, caseId, item => item.id, "case");
        }

        internal static FixtureExpectationRecord FindExpectation(
            FixtureExpectationCatalog expectations,
            string caseId)
        {
            Require(expectations != null, "Fixture expectation catalog is missing.");
            return FindUnique(expectations.cases, caseId, item => item.caseId, "expectation case");
        }

        private static void ValidateTexture(TextureFixtureRecord texture)
        {
            Require(texture.width > 0 && texture.height > 0,
                $"Texture {texture.id} has invalid dimensions.");
            Require(texture.alpha8BottomToTop != null,
                $"Texture {texture.id} has no alpha values.");
            Require(texture.alpha8BottomToTop.Length == (long)texture.width * texture.height,
                $"Texture {texture.id} has the wrong alpha value count.");

            foreach (var alpha in texture.alpha8BottomToTop)
            {
                Require(alpha >= 0 && alpha <= 255,
                    $"Texture {texture.id} has an alpha value outside [0, 255].");
            }
        }

        private static void ValidateMesh(MeshFixtureRecord mesh)
        {
            Require(mesh.positions != null && mesh.positions.Length >= 9 && mesh.positions.Length % 3 == 0,
                $"Mesh {mesh.id} has invalid positions.");

            for (var index = 0; index < mesh.positions.Length; index++)
            {
                Require(IsFinite(mesh.positions[index]),
                    $"Mesh {mesh.id} has a non-finite position.");
                if (index % 3 == 2)
                {
                    Require(mesh.positions[index] == 0f,
                        $"Mesh {mesh.id} has a non-planar position.");
                }
            }

            Require(UvStates.Contains(mesh.uv0Status),
                $"Mesh {mesh.id} has an invalid UV0 state: {mesh.uv0Status}");
            Require(mesh.uv0 != null, $"Mesh {mesh.id} has no UV0 array.");
            var vertexCount = mesh.positions.Length / 3;
            if (mesh.uv0Status == "Present")
            {
                Require(mesh.uv0.Length == vertexCount * 2,
                    $"Mesh {mesh.id} has the wrong UV0 value count.");
                foreach (var value in mesh.uv0)
                {
                    Require(IsFinite(value), $"Mesh {mesh.id} has a non-finite UV0 value.");
                }
            }
            else
            {
                Require(mesh.uv0.Length == 0, $"Mesh {mesh.id} has UV0 values while missing UV0.");
            }

            Require(mesh.triangleVertexIndices != null && mesh.triangleVertexIndices.Length != 0 &&
                mesh.triangleVertexIndices.Length % 3 == 0,
                $"Mesh {mesh.id} has invalid triangle indices.");
            foreach (var vertexIndex in mesh.triangleVertexIndices)
            {
                Require(vertexIndex >= 0 && vertexIndex < vertexCount,
                    $"Mesh {mesh.id} has an out-of-range triangle index.");
            }

            for (var triangle = 0; triangle < mesh.triangleVertexIndices.Length; triangle += 3)
            {
                var p0 = mesh.triangleVertexIndices[triangle] * 3;
                var p1 = mesh.triangleVertexIndices[triangle + 1] * 3;
                var p2 = mesh.triangleVertexIndices[triangle + 2] * 3;
                var signedDoubleArea =
                    ((double)mesh.positions[p1] - mesh.positions[p0]) *
                    (mesh.positions[p2 + 1] - mesh.positions[p0 + 1]) -
                    ((double)mesh.positions[p1 + 1] - mesh.positions[p0 + 1]) *
                    (mesh.positions[p2] - mesh.positions[p0]);
                Require(signedDoubleArea >= 0d,
                    $"Mesh {mesh.id} has a clockwise triangle.");
            }
        }

        private static void ValidateOutcomes(FixtureExpectationRecord expectation, int triangleCount)
        {
            Require(expectation.triangleOutcomes != null,
                $"Expectation case {expectation.caseId} has no triangle outcomes.");
            var triangleIndices = new HashSet<int>();
            foreach (var triangleOutcome in expectation.triangleOutcomes)
            {
                Require(triangleOutcome != null,
                    $"Expectation case {expectation.caseId} has a missing triangle outcome.");
                Require(triangleOutcome.triangleIndex >= 0 &&
                    triangleOutcome.triangleIndex < triangleCount,
                    $"Expectation case {expectation.caseId} has an out-of-range triangle index.");
                Require(triangleIndices.Add(triangleOutcome.triangleIndex),
                    $"Expectation case {expectation.caseId} has a duplicate triangle index.");
                Require(Outcomes.Contains(triangleOutcome.outcome),
                    $"Expectation case {expectation.caseId} has an invalid outcome: {triangleOutcome.outcome}");
            }

            Require(triangleIndices.Count == triangleCount,
                $"Expectation case {expectation.caseId} does not cover every triangle.");
        }

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

        private static T FindUnique<T>(
            T[] records,
            string id,
            Func<T, string> selectId,
            string kind)
            where T : class
        {
            Require(records != null, $"{kind} records are missing.");
            T match = null;
            foreach (var record in records)
            {
                Require(record != null, $"{kind} record is missing.");
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

        private static void AddRecordId(HashSet<string> ids, string id, string kind)
        {
            Require(!string.IsNullOrEmpty(id), $"{kind} ID is empty.");
            Require(ids.Add(id), $"Duplicate {kind} ID: {id}");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(message);
            }
        }
    }
}
