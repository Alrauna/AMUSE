using System;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;

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

        // --- Falsifier 3: total-instead-of-proven ---

        [Test]
        public void SplitSentenceShowsProvenTrianglesFirst()
        {
            // A swapped ordinal pair draws the counts backwards and the
            // reader cannot tell which share the proof covered.
            Assert.That(
                RowText.SplitRowSentence(2, 12, 40),
                Is.EqualTo("slot 2: would split. Triangles proven: 12 of 40."));
        }

        [Test]
        public void FullyUnprovenSlotNeverClaimsSplit()
        {
            // A builder that reads the total where the proven count
            // belongs would claim a split for a slot with nothing
            // proven. The spec words that case as the unchanged row.
            Assert.That(
                RowText.SplitRowSentence(2, 0, 40),
                Is.EqualTo(
                    "slot 2: unchanged. Triangles proven: 0 of 40. " +
                    "The share is below the convert setting."));
        }

        [Test]
        public void FoldRowNamesThePreserveSetting()
        {
            Assert.That(
                RowText.FoldRowSentence(3),
                Is.EqualTo(
                    "slot 3: would fold onto the opaque copy. " +
                    "The unproven share is below the preserve setting."));
        }

        [Test]
        public void AlreadyOpaqueRowKeepsTheSlotAsItIs()
        {
            Assert.That(
                RowText.AlreadyOpaqueRowSentence(1),
                Is.EqualTo("slot 1: already opaque. It stays as it is."));
        }

        [Test]
        public void UnchangedRowShowsTheProvenShare()
        {
            Assert.That(
                RowText.UnchangedRowSentence(4, 5, 100),
                Is.EqualTo(
                    "slot 4: unchanged. Triangles proven: 5 of 100. " +
                    "The share is below the convert setting."));
        }

        [Test]
        public void RefusedSlotRowUsesTheTableSentence()
        {
            // The refusal text lives in the shared table, so the row
            // builder formats the key the capture records.
            var key = "amuse.texture.UnsupportedFormat";
            Assert.That(
                RowText.RefusedRowSentence(0, key),
                Is.EqualTo(
                    "slot 0: refused. " +
                    AmuseReportStrings.Format(key)));
        }

        [Test]
        public void RefusedSlotRowThrowsOnUnknownKey()
        {
            // The dry run guards with Has before it renders. A builder
            // that renders an empty refusal instead of throwing hides a
            // broken table behind a blank inspector row.
            Assert.Throws<InvalidOperationException>(
                () => RowText.RefusedRowSentence(0, "amuse.no.such.key"));
        }

        [Test]
        public void RendererSummaryShowsCountsAndRefusedSlots()
        {
            // The mixed-refusal summary: movement, proof, and the
            // refused share stay visible as one line, so missing slot
            // rows read as a difference against the real slot count.
            Assert.That(
                RowText.RendererSummarySentence(
                    Path, TypeName,
                    moved: 300, proven: 120, total: 400,
                    refusedSlots: 1, slotCount: 4,
                    foldContributed: false),
                Is.EqualTo(
                    "Body (SkinnedMeshRenderer): " +
                    "Triangles moved: 300. Triangles proven: 120 of 400. " +
                    "1 of 4 slots refused."));
        }

        [Test]
        public void RendererSummaryAppendsFoldSentenceWhenFoldContributed()
        {
            // A fold moves unproven triangles, so the row must name the
            // policy that moves them.
            Assert.That(
                RowText.RendererSummarySentence(
                    Path, TypeName,
                    moved: 300, proven: 120, total: 400,
                    refusedSlots: 1, slotCount: 4,
                    foldContributed: true),
                Is.EqualTo(
                    "Body (SkinnedMeshRenderer): " +
                    "Triangles moved: 300. Triangles proven: 120 of 400. " +
                    "1 of 4 slots refused. " +
                    "The preserve policy moves the rest."));
        }

        [Test]
        public void RendererNoMovementShowsTheProvenShare()
        {
            Assert.That(
                RowText.RendererNoMovementSentence(Path, TypeName, 90, 400),
                Is.EqualTo(
                    "Body (SkinnedMeshRenderer): no movement. " +
                    "Triangles proven: 90 of 400."));
        }

        [Test]
        public void RendererNoChangeShowsNothingProven()
        {
            Assert.That(
                RowText.RendererNoChangeSentence("Hair", "MeshRenderer"),
                Is.EqualTo("Hair (MeshRenderer): no change. Nothing proven."));
        }

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
        }

        [Test]
        public void DisclosureSentencesMatchTheSpec()
        {
            Assert.That(
                RowText.DisclosureSentence(0),
                Is.EqualTo(
                    "This view runs the same proof on the avatar in the " +
                    "scene. Other tools change the avatar later at build " +
                    "time. This view cannot see those changes. A tool can " +
                    "also add animations the build must obey. Then the " +
                    "build proves less than this view shows. A run in " +
                    "Play mode or an upload shows the true result."));
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
            Assert.That(row.RefusedSlots, Is.EqualTo(-1));
            Assert.That(row.SlotCount, Is.EqualTo(-1));
            Assert.That(row.Sentence, Is.EqualTo("errored sentence"));
        }

        [Test]
        public void RendererFactoryCarriesEveryCount()
        {
            var row = MaterialTransformationRow.Renderer(
                Path, TypeName,
                moved: 300, proven: 120, total: 400,
                refusedSlots: 1, slotCount: 4,
                sentence: "summary sentence");
            Assert.That(row.Kind, Is.EqualTo(MaterialTransformationRowKind.Renderer));
            Assert.That(row.RendererPath, Is.EqualTo(Path));
            Assert.That(row.RendererTypeName, Is.EqualTo(TypeName));
            Assert.That(row.SlotIndex, Is.EqualTo(-1));
            Assert.That(row.MovedTriangles, Is.EqualTo(300));
            Assert.That(row.ProvenTriangles, Is.EqualTo(120));
            Assert.That(row.TotalTriangles, Is.EqualTo(400));
            Assert.That(row.RefusedSlots, Is.EqualTo(1));
            Assert.That(row.SlotCount, Is.EqualTo(4));
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
            Assert.That(row.RefusedSlots, Is.EqualTo(-1));
            Assert.That(row.SlotCount, Is.EqualTo(-1));
            Assert.That(row.Sentence, Is.EqualTo("slot sentence"));
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
                    Path, TypeName, 1, 2, 3, 0, 1, ""));
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
                RowText.SplitRowSentence(2, 0, 40),
                RowText.SplitRowSentence(2, 12, 40),
                RowText.FoldRowSentence(3),
                RowText.AlreadyOpaqueRowSentence(1),
                RowText.UnchangedRowSentence(4, 5, 100),
                RowText.RefusedRowSentence(0, "amuse.texture.UnsupportedFormat"),
                RowText.RendererSummarySentence(
                    Path, TypeName, 300, 120, 400, 1, 4, false),
                RowText.RendererSummarySentence(
                    Path, TypeName, 300, 120, 400, 1, 4, true),
                RowText.RendererNoMovementSentence(Path, TypeName, 90, 400),
                RowText.RendererNoChangeSentence(Path, TypeName),
                RowText.RendererRefusedSentence(
                    Path, TypeName, "amuse.renderer.MissingMesh"),
                RowText.StructuralRowSentence("", "avatar refusal"),
                RowText.BannerSentence(BannerKind.AmuseOff),
                RowText.BannerSentence(BannerKind.NoSamplingPolicy),
                RowText.NoteSentence(NoteKind.StreamingMipmaps),
                RowText.NoteSentence(NoteKind.RefreshOnly),
                RowText.DisclosureSentence(0),
                RowText.DisclosureSentence(1),
                RowText.ErroredSentence(Path, TypeName),
                RowText.NoRenderersSentence(),
            };

            foreach (var sentence in sentences)
            {
                Assert.That(sentence, Is.Not.Null.And.Not.Empty);
            }
        }
    }
}
