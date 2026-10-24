using System;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.DryRun
{
    /// <summary>
    /// The row model is the cache's stored unit and the inspector's draw
    /// input. It is pure, so every sentence is pinned verbatim here. An
    /// unproven triangle must never display as proven, so the split
    /// builder carries the unproven-slot guard. An empty sentence is a
    /// test failure, never a displayable row, so the table-sourced
    /// builders guard the string table with Has before they render.
    /// </summary>
    public sealed class MaterialTransformationRowTests
    {
        private const string Path = "Body";
        private const string TypeName = "SkinnedMeshRenderer";

        [Test]
        public void RendererRefusedRowUsesTheTableSentence()
        {
            var key = "amuse.renderer.MissingMesh";
            Assert.That(
                RowText.RendererRefusedSentence(Path, TypeName, key),
                Is.EqualTo(
                    "Body (SkinnedMeshRenderer): refused. " +
                    AmuseReportStrings.Format(key)));
        }

        [Test]
        public void RendererRefusedRowThrowsOnUnknownKey()
        {
            Assert.Throws<InvalidOperationException>(
                () => RowText.RendererRefusedSentence(
                    Path, TypeName, "amuse.no.such.key"));
        }

        [Test]
        public void StructuralRowPrefixesTheRendererName()
        {
            // The renderer name joins ahead of the avatar refusal
            // sentence when one renderer caused it, and the bare
            // sentence stands alone when the refusal is avatar wide.
            Assert.That(
                RowText.StructuralRowSentence(
                    "'Arm' in 'Body': ", "The graph refused the controllers."),
                Is.EqualTo(
                    "'Arm' in 'Body': The graph refused the controllers."));
            Assert.That(
                RowText.StructuralRowSentence("", "The graph refused the controllers."),
                Is.EqualTo("The graph refused the controllers."));
        }

        [Test]
        public void StructuralRowThrowsOnEmptySentence()
        {
            Assert.Throws<ArgumentException>(
                () => RowText.StructuralRowSentence("", ""));
        }

        [Test]
        public void BannerSentencesMatchTheSpec()
        {
            Assert.That(
                RowText.BannerSentence(BannerKind.AmuseOff),
                Is.EqualTo("AMUSE is off. Turn it on to see results."));
            Assert.That(
                RowText.BannerSentence(BannerKind.SeparatorOff),
                Is.EqualTo(
                    "The Alpha Separator is off. Nothing will move. " +
                    "Turn it on to see results."));
            Assert.That(
                RowText.BannerSentence(BannerKind.RootRequired),
                Is.EqualTo(
                    "This component must sit on the avatar root object."));
            Assert.That(
                RowText.BannerSentence(BannerKind.NonWindowsTarget),
                Is.EqualTo(
                    "The active build target is not Windows. Texture proof " +
                    "needs the Windows import. This view can prove fewer " +
                    "triangles than a Windows build."));
            Assert.That(
                RowText.BannerSentence(BannerKind.NoSamplingPolicy),
                Is.EqualTo(
                    "The active build target supports no sampling policy. " +
                    "A build on this target applies nothing. This view " +
                    "shows no results for it."));
        }

        [Test]
        public void NoteSentencesMatchTheSpec()
        {
            Assert.That(
                RowText.NoteSentence(NoteKind.StreamingMipmaps),
                Is.EqualTo(
                    "Some textures use streaming mipmaps. This view does " +
                    "not read them, because reading would change project " +
                    "files. A build can prove more for those textures."));
            Assert.That(
                RowText.NoteSentence(NoteKind.LockedMaterials),
                Is.EqualTo(
                    "This avatar holds locked or transfer protected " +
                    "materials. A build with granted consent may convert " +
                    "further than this view shows."));
            Assert.That(
                RowText.NoteSentence(NoteKind.EditorOnly),
                Is.EqualTo(
                    "Some objects are marked EditorOnly. The build removes " +
                    "them before AMUSE runs. This view shows what those " +
                    "renderers would get if they stayed."));
            Assert.That(
                RowText.NoteSentence(NoteKind.RefreshOnly),
                Is.EqualTo(
                    "Some textures change every frame. This view computes " +
                    "them on Refresh only."));
            Assert.That(
                RowText.NoteSentence(NoteKind.ApprovedUnverifiedSource),
                Is.EqualTo(
                    "A shader version inside the supported range did not " +
                    "verify against its measured source. Its triangles " +
                    "stay unproven."));
            Assert.That(
                RowText.NoteSentence(NoteKind.LockedMaterialNotReproducible),
                Is.EqualTo(
                    "Some locked materials refuse in a build. The cause " +
                    "can be the recorded source, a property block, or an " +
                    "animation. This view does not promise a conversion " +
                    "for those materials."));
        }

        [Test]
        public void DisclosureSentencesMatchTheSpec()
        {
            Assert.That(
                RowText.DisclosureSentence(0),
                Is.EqualTo(
                    "This view runs the same proof as a build on the avatar in the " +
                    "scene. Other tools can change the avatar during build time. " +
                    "Run a Play mode build and exit to see accurate information."));
            Assert.That(
                RowText.DisclosureSentence(1),
                Is.EqualTo(
                    "The first open measures textures on the GPU. " +
                    "It can take a moment."));
        }

        [Test]
        public void DisclosureSentenceThrowsOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RowText.DisclosureSentence(2));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RowText.DisclosureSentence(-1));
        }

        [Test]
        public void ErroredSentenceNamesTheRendererAndRefresh()
        {
            Assert.That(
                RowText.ErroredSentence(Path, TypeName),
                Is.EqualTo(
                    "Body (SkinnedMeshRenderer): could not be computed. " +
                    "Press Refresh to try again."));
        }

        [Test]
        public void NoRenderersSentenceMatchesTheSpec()
        {
            Assert.That(
                RowText.NoRenderersSentence(),
                Is.EqualTo("No renderer on this avatar."));
        }

        [Test]
        public void AvatarLevelFactoriesCarryNoRendererCounts()
        {
            var banner = MaterialTransformationRow.Banner("banner");
            Assert.That(banner.Kind, Is.EqualTo(MaterialTransformationRowKind.Banner));
            var note = MaterialTransformationRow.Note("note");
            Assert.That(note.Kind, Is.EqualTo(MaterialTransformationRowKind.Note));
            Assert.That(banner.Sentence, Is.Not.Null.And.Not.Empty);
            Assert.That(note.Sentence, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ErroredFactoryCarriesTheRenderer()
        {
            var row = MaterialTransformationRow.Errored(
                Path, TypeName, "errored sentence");
            Assert.That(row.Kind, Is.EqualTo(MaterialTransformationRowKind.Errored));
            Assert.That(row.RendererPath, Is.EqualTo(Path));
            Assert.That(row.RendererTypeName, Is.EqualTo(TypeName));
            Assert.That(row.SlotIndex, Is.EqualTo(-1));
            Assert.That(row.MovedTriangles, Is.EqualTo(-1));
            Assert.That(row.ProvenTriangles, Is.EqualTo(-1));
            Assert.That(row.TotalTriangles, Is.EqualTo(-1));
            Assert.That(row.Sentence, Is.EqualTo("errored sentence"));
        }

        [Test]
        public void RendererFactoryCarriesEveryCount()
        {
            var row = MaterialTransformationRow.Renderer(
                Path, TypeName,
                moved: 300, proven: 120, total: 400,
                sentence: "summary sentence");
            Assert.That(row.Kind, Is.EqualTo(MaterialTransformationRowKind.Renderer));
            Assert.That(row.RendererPath, Is.EqualTo(Path));
            Assert.That(row.RendererTypeName, Is.EqualTo(TypeName));
            Assert.That(row.SlotIndex, Is.EqualTo(-1));
            Assert.That(row.MovedTriangles, Is.EqualTo(300));
            Assert.That(row.ProvenTriangles, Is.EqualTo(120));
            Assert.That(row.TotalTriangles, Is.EqualTo(400));
            Assert.That(row.Sentence, Is.EqualTo("summary sentence"));
        }

        [Test]
        public void SlotFactoryCarriesItsSlotCounts()
        {
            var row = MaterialTransformationRow.Slot(
                Path, TypeName, slotIndex: 2,
                proven: 12, total: 40,
                sentence: "slot sentence");
            Assert.That(row.Kind, Is.EqualTo(MaterialTransformationRowKind.Slot));
            Assert.That(row.RendererPath, Is.EqualTo(Path));
            Assert.That(row.RendererTypeName, Is.EqualTo(TypeName));
            Assert.That(row.SlotIndex, Is.EqualTo(2));
            Assert.That(row.MovedTriangles, Is.EqualTo(-1));
            Assert.That(row.ProvenTriangles, Is.EqualTo(12));
            Assert.That(row.TotalTriangles, Is.EqualTo(40));
            Assert.That(row.Sentence, Is.EqualTo("slot sentence"));
        }

        // --- Stored numeric row-kind wire contract ---

        // The fixtures carry the wire numbers. They never name the
        // enum. The assertion names the enum. This pins the stored
        // numeric payload contract. The values must stay 0 through 4.

        internal static string SerializedRowJson(int kind)
        {
            switch (kind)
            {
                case 0:
                    return "{\"kind\":0,\"sentence\":" +
                        "\"AMUSE is off. Turn it on to see results.\"}";
                case 1:
                    return "{\"kind\":1,\"sentence\":" +
                        "\"Some textures use streaming mipmaps.\"}";
                case 2:
                    return "{\"kind\":2," +
                        "\"rendererPath\":\"Armature/Body\"," +
                        "\"rendererTypeName\":\"UnityEngine.SkinnedMeshRenderer\"," +
                        "\"movedTriangles\":300,\"provenTriangles\":120," +
                        "\"totalTriangles\":400," +
                        "\"sentence\":\"Armature/Body summary row.\"}";
                case 3:
                    return "{\"kind\":3," +
                        "\"rendererPath\":\"Armature/Body\"," +
                        "\"rendererTypeName\":\"UnityEngine.SkinnedMeshRenderer\"," +
                        "\"sourceMaterialPath\":" +
                        "\"Assets/AmuseTests_NumberedBody.mat\"," +
                        "\"slotIndex\":1,\"provenTriangles\":50," +
                        "\"totalTriangles\":100," +
                        "\"sentence\":" +
                        "\"Splittable: 50 out of 100 polygons may be moved\"}";
                case 4:
                    return "{\"kind\":4," +
                        "\"rendererPath\":\"Armature/Body\"," +
                        "\"rendererTypeName\":\"UnityEngine.SkinnedMeshRenderer\"," +
                        "\"sentence\":\"Body (SkinnedMeshRenderer): could not " +
                        "be computed. Press Refresh to try again.\"}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void StoredNumericKindReadsBackAsItsTypedRowKind(
            int wireKind)
        {
            // The test maps each wire number to the named enum
            // member here. The reader must land on the same member.
            MaterialTransformationRowKind expectedKind;
            switch (wireKind)
            {
                case 0:
                    expectedKind = MaterialTransformationRowKind.Banner;
                    break;
                case 1:
                    expectedKind = MaterialTransformationRowKind.Note;
                    break;
                case 2:
                    expectedKind = MaterialTransformationRowKind.Renderer;
                    break;
                case 3:
                    expectedKind = MaterialTransformationRowKind.Slot;
                    break;
                case 4:
                    expectedKind = MaterialTransformationRowKind.Errored;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(wireKind));
            }

            var dto = JsonUtility.FromJson<SerializedTransformationRow>(
                SerializedRowJson(wireKind));
            var row = MaterialTransformationRow.FromSerialized(dto);

            Assert.That(
                row.Kind,
                Is.EqualTo(expectedKind),
                "stored numeric kind " + wireKind +
                " must read back as its typed row kind");
            Assert.That(
                row.Sentence,
                Is.Not.Null.And.Not.Empty,
                "the read back row must keep its payload sentence");
            switch (wireKind)
            {
                case 2:
                    Assert.That(row.TotalTriangles, Is.EqualTo(400));
                    break;
                case 3:
                    Assert.That(row.SlotIndex, Is.EqualTo(1));
                    break;
                case 4:
                    Assert.That(row.RendererPath, Is.EqualTo("Armature/Body"));
                    break;
            }
        }

        [Test]
        public void FactoriesRejectANullOrEmptySentence()
        {
            Assert.Throws<ArgumentException>(
                () => MaterialTransformationRow.Banner(null));
            Assert.Throws<ArgumentException>(
                () => MaterialTransformationRow.Note(""));
            Assert.Throws<ArgumentException>(
                () => MaterialTransformationRow.Errored(Path, TypeName, null));
            Assert.Throws<ArgumentException>(
                () => MaterialTransformationRow.Renderer(
                    Path, TypeName, 1, 2, 3, ""));
            Assert.Throws<ArgumentException>(
                () => MaterialTransformationRow.Slot(
                    Path, TypeName, 0, 1, 2, null));
        }

        [Test]
        public void BuildersNeverReturnANullOrEmptySentence()
        {
            // Guard for the falsifier above: no builder may hand the
            // inspector a blank row, whatever the counts say.
            var sentences = new[]
            {
                RowText.StructuralRowSentence("", "avatar refusal"),
                RowText.BannerSentence(BannerKind.AmuseOff),
                RowText.BannerSentence(BannerKind.NoSamplingPolicy),
                RowText.NoteSentence(NoteKind.StreamingMipmaps),
                RowText.NoteSentence(NoteKind.RefreshOnly),
                RowText.DisclosureSentence(0),
                RowText.DisclosureSentence(1),
                RowText.ErroredSentence(Path, TypeName),
                RowText.NoRenderersSentence(),
                RowText.RefusedSlotSentence(),
                RowText.UntouchableSlotSentence(100),
                RowText.SplittableSlotSentence(40, 100),
                RowText.ConvertableSlotSentence(100),
                RowText.DeferredSlotSentence(100),
            };

            foreach (var sentence in sentences)
            {
                Assert.That(sentence, Is.Not.Null.And.Not.Empty);
            }
        }

        // --- Simplified transformation sentence tests ---

        [Test]
        public void RefusedSlotSentenceMatchesSimplifiedSpec()
        {
            Assert.That(
                RowText.RefusedSlotSentence(),
                Is.EqualTo("Refused: AMUSE currently does not support this material"));
        }

        [Test]
        public void UntouchableSlotSentenceMatchesSimplifiedSpec()
        {
            Assert.That(
                RowText.UntouchableSlotSentence(100),
                Is.EqualTo("Untouchable: 0 out of 100 polygons may be moved"));
        }

        [Test]
        public void UntouchableSlotSentenceHandlesZeroPolygons()
        {
            Assert.That(
                RowText.UntouchableSlotSentence(0),
                Is.EqualTo("Untouchable: 0 out of 0 polygons may be moved"));
        }

        [Test]
        public void SplittableSlotSentenceMatchesSimplifiedSpec()
        {
            Assert.That(
                RowText.SplittableSlotSentence(40, 100),
                Is.EqualTo("Splittable: 40 out of 100 polygons may be moved"));
        }

        [Test]
        public void ConvertableSlotSentenceMatchesSimplifiedSpec()
        {
            Assert.That(
                RowText.ConvertableSlotSentence(100),
                Is.EqualTo("Convertable: 100 out of 100 polygons may be moved"));
        }

        [Test]
        public void ConvertableSlotSentenceHandlesZeroPolygons()
        {
            Assert.That(
                RowText.ConvertableSlotSentence(0),
                Is.EqualTo("Convertable: 0 out of 0 polygons may be moved"));
        }

        [Test]
        public void DeferredSlotSentenceFormatsWithTotalCount()
        {
            var sentence = RowText.DeferredSlotSentence(1234);
            Assert.That(
                sentence,
                Is.EqualTo("Deferred: 0 out of 1234 polygons may be moved (evaluates at build time)"));
        }

        [Test]
        public void DeferredSlotSentenceHandlesZeroCount()
        {
            var sentence = RowText.DeferredSlotSentence(0);
            Assert.That(
                sentence,
                Is.EqualTo("Deferred: 0 out of 0 polygons may be moved (evaluates at build time)"));
        }
    }
}
