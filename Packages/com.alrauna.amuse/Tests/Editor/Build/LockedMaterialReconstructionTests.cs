using System.IO;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The in-memory locked-material reconstruction on locked shader
    /// pairs. Every fixture is a LockedSourceFixture pair: the shipped
    /// semantic test shader plays the recorded original, the written
    /// partner plays the locked generated shader, and the locked material
    /// carries the locked serialization and tags. No repo content outside
    /// the fixture folder is written or refreshed.
    /// </summary>
    public sealed class LockedMaterialReconstructionTests
    {
        // The fixed refusal sentences the reconstruction contract pins.
        private const string NameTagMissingDetail =
            "the recorded original shader name tag is missing";
        private const string GuidTagMissingDetail =
            "the recorded original shader GUID tag is missing";
        private const string GuidUnresolvableDetail =
            "the recorded original shader GUID does not resolve to a " +
            "shader asset";
        private const string AttestationFailedDetail =
            "the resolved original shader did not pass attestation";
        private const string FlagPropertyMissingDetail =
            "the resolved original shader does not declare the " +
            "optimizer flag property";
        private const string CompileErrorDetail =
            "The generated shader of the locked material does not " +
            "compile.";
        private const string PropDefineDetail =
            "The texture defines of a program block do not match the " +
            "textures of the material.";

        // A GUID-shaped marker that resolves to no asset. It is a
        // synthetic constant, not the GUID of any real asset.
        private const string UnresolvableGuid =
            "deadbeefdeadbeefdeadbeefdeadbeef";

        private readonly TestTransientScope _scope =
            new TestTransientScope(allowDestroyingAssets: true);

        [SetUp]
        public void SetUp()
        {
            LockedSourceFixture.DeleteFolder();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
            LockedSourceFixture.DeleteFolder();
            AssetDatabase.Refresh();
        }

        private T Track<T>(T obj) where T : Object
        {
            if (obj != null)
            {
                _scope.Track(obj);
            }

            return obj;
        }

        private static TransientUnlockRestoreResult Apply(
            Material locked, bool attests)
        {
            return LockedMaterialReconstruction.TryApply(
                locked, _ => attests);
        }

        private static void AssertRefusal(
            TransientUnlockRestoreResult result,
            string expectedDetail,
            string recordedGuid)
        {
            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Failed),
                "the reconstruction must refuse");
            Assert.That(result.Detail, Is.EqualTo(expectedDetail));
            Assert.That(result.Detail.Contains("/"), Is.False,
                "a refusal detail must never carry a path");
            Assert.That(result.Detail.Contains(recordedGuid), Is.False,
                "a refusal detail must never carry a GUID");
        }

        /// <summary>
        /// The guard of every falsifier: the pair aligns and the
        /// reproduction builds a non-empty plan before the test applies
        /// its edit. A guard failure means the fixture broke, never the
        /// falsified behavior.
        /// </summary>
        private static void AssertGuard(
            LockedSourceFixture fixture, Material locked)
        {
            var originalText = LockedMaterialIdentity.ReadOriginalText(
                fixture.Original);
            var generatedText = LockedMaterialIdentity.ReadOriginalText(
                fixture.Partner);
            Assert.That(originalText, Is.Not.Null,
                "the original text must be readable for the guard");
            Assert.That(generatedText, Is.Not.Null,
                "the generated text must be readable for the guard");
            var properties = LockedSourceAlignmentMemo.PropertiesOf(
                fixture.Original, originalText);
            var aligned = LockedSourceAlignmentMemo.GetOrAlign(
                fixture.Partner, fixture.Original, properties);
            Assert.That(aligned.Succeeded, Is.True,
                "fixture guard: the pair must align before the edit, " +
                "but the aligner refused at original line " +
                aligned.OriginalLine + " with " +
                (aligned.RefusalDetail ?? "no sentence"));

            var lockedView = new UnityLockedMaterialValues(locked);
            var reproduction = LockedSourceReproduction.Build(
                aligned.Source, properties, lockedView);
            Assert.That(reproduction.Succeeded, Is.True,
                "fixture guard: the plan must build before the edit, " +
                "but the reproduction refused with " +
                (reproduction.RefusalDetail ?? "no sentence"));
            Assert.That(reproduction.Plan.FloatWrites.Count +
                    reproduction.Plan.VectorWrites.Count +
                    reproduction.Plan.ColorWrites.Count +
                    reproduction.Plan.TextureWrites.Count,
                Is.GreaterThan(0),
                "fixture guard: the plan must hold at least one write");
        }

        // --- Falsifier 1: an implementation that derives the rename from
        // the material name instead of the generated text and misses a
        // cleaned underscore suffix, or that cannot carry an edited
        // renamed value onto the plain name. The suffix of the material
        // name U_Cape is U5FCape, and the renamed alpha input is edited
        // after the lock. The plain color must take the edited renamed
        // value.
        [Test]
        public void UnderscoreNameWithAnEditedRenamedAlphaInputReproducesTheLockedValue()
        {
            var fixture = new LockedSourceFixture();
            fixture.WithRename("U5FCape");
            var locked = Track(fixture.Locked("U_Cape"));
            AssertGuard(fixture, locked);

            // The partner declares the renamed name, so the edit after
            // the lock is a typed write on a declared slot, exactly the
            // way an authored material holds it.
            var renamedColor = "_Color_U5FCape";
            var edited = new Color(0.25f, 0.5f, 0.75f, 0.125f);
            locked.SetColor(renamedColor, edited);
            var expected = locked.GetColor(renamedColor);
            Assert.That(expected.a, Is.EqualTo(0.125f),
                "fixture guard: the edited renamed value must be " +
                "readable on the locked material");

            var result = Apply(locked, true);

            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                result.Detail ?? "the reconstruction refused");
            Assert.That(result.Detail, Is.Null);
            Assert.That(locked.shader, Is.SameAs(fixture.Original),
                "the reconstruction must rebind the recorded original");
            Assert.That(locked.GetColor("_Color"),
                Is.EqualTo(expected),
                "the plain name must take the edited renamed value");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f),
                "the lock flag must clear after the reconstruction");
        }

        // --- Falsifier 2: an implementation that gates the renamed move
        // on the per-property Animated tag. The pair renames the texture,
        // the material carries no Animated tag at all, and the renamed
        // value must still land on the plain name.
        [Test]
        public void MissingAnimatedTagsDoNotStopTheRenamedMove()
        {
            var fixture = new LockedSourceFixture();
            fixture.WithRename("XCape");
            var locked = Track(fixture.Locked("X Cape"));
            AssertGuard(fixture, locked);

            var texture = Track(new Texture2D(4, 4));
            var renamedTexture = "_MainTex_XCape";
            locked.SetTexture(renamedTexture, texture);
            locked.SetTextureScale(renamedTexture, new Vector2(2f, 3f));
            locked.SetTextureOffset(renamedTexture, new Vector2(0.25f, 0.5f));

            var result = Apply(locked, true);

            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                result.Detail ?? "the reconstruction refused");
            Assert.That(locked.GetTexture("_MainTex"),
                Is.SameAs(texture),
                "the renamed texture must land on the plain name");
            Assert.That(locked.GetTextureScale("_MainTex"),
                Is.EqualTo(new Vector2(2f, 3f)),
                "the renamed scale must land on the plain name");
            Assert.That(locked.GetTextureOffset("_MainTex"),
                Is.EqualTo(new Vector2(0.25f, 0.5f)),
                "the renamed offset must land on the plain name");
        }

        // --- Falsifier 3: an implementation that trusts a rename suffix
        // tag. The fixture writes both suffix tags with values that
        // disagree with the real suffix in the generated text. The move
        // must still happen, because the text is the only truth source.
        [Test]
        public void LockedRenameSuffixTagThatDisagreesWithTheNameIsIgnored()
        {
            var fixture = new LockedSourceFixture();
            fixture.WithRename("RealSuffix");
            var locked = Track(fixture.Locked("Plain Name"));
            fixture.WriteDecoySuffixTags(locked, "WrongSuffix");
            AssertGuard(fixture, locked);

            var renamedColor = "_Color_RealSuffix";
            var edited = new Color(0.5f, 0.25f, 0.75f, 0.5f);
            locked.SetColor(renamedColor, edited);
            var expected = locked.GetColor(renamedColor);

            var result = Apply(locked, true);

            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                result.Detail ?? "the reconstruction refused");
            Assert.That(locked.GetColor("_Color"),
                Is.EqualTo(expected),
                "the suffix tags must be ignored and the renamed value " +
                "must move");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f),
                "every other step must still have held");
        }

        // --- Falsifier 4: an implementation that carries the live saved
        // value of a baked property instead of the baked literal. The
        // original gains one program use of _AlphaForceOpaque, the
        // partner bakes it to the vendor print of one, and the locked
        // material is edited to zero after the lock. The clone must take
        // the baked value.
        [Test]
        public void BakedForceOpaqueEditedAfterLockTakesTheBakedValue()
        {
            var fixture = new LockedSourceFixture();
            fixture.WithProgramHeadLines(
                "            float _AlphaForceOpaque;");
            fixture.WithFragmentLines(
                "            i.uv.x += _AlphaForceOpaque;");
            fixture.WithBake("_AlphaForceOpaque", "1.0");
            var locked = Track(fixture.Locked("BakedCape"));
            AssertGuard(fixture, locked);

            locked.SetFloat("_AlphaForceOpaque", 0f);

            var result = Apply(locked, true);

            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                result.Detail ?? "the reconstruction refused");
            Assert.That(locked.shader, Is.SameAs(fixture.Original));
            Assert.That(locked.GetFloat("_AlphaForceOpaque"),
                Is.EqualTo(1f),
                "the baked literal must win over the edited saved " +
                "value");
        }

        // --- Falsifier 5: an implementation that restores a mask
        // assigned after the lock without its texture define. The
        // original holds a surviving masked block under the define, and
        // the generated text carries no PROP define for the mask,
        // because the mask slot was empty at lock time. A mask assigned
        // after the lock must refuse instead of restoring.
        [Test]
        public void MaskAssignedAfterLockWithoutItsDefineRefuses()
        {
            var fixture = new LockedSourceFixture();
            fixture.WithProgramLines(
                "            #if defined(PROP_ALPHAMASK)",
                "            sampler2D _AlphaMask;",
                "            #endif");
            var locked = Track(fixture.Locked("MaskCape"));
            AssertGuard(fixture, locked);

            // The mask is assigned after the lock, on the live locked
            // material. The generated text carries no PROP define for it.
            var mask = Track(new Texture2D(4, 4));
            locked.SetTexture("_AlphaMask", mask);

            var result = Apply(locked, true);

            AssertRefusal(result, PropDefineDetail, UnresolvableGuid);
            Assert.That(locked.shader, Is.SameAs(fixture.Partner),
                "a refused reconstruction must not swap the shader");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f),
                "a refused reconstruction must not clear the lock flag");
        }

        // --- Falsifier 6: an implementation that tolerates a generated
        // label line that differs from the original. The shipped label
        // stays the release label, so a changed label must refuse.
        [Test]
        public void DifferentLabelLineRefuses()
        {
            var fixture = new LockedSourceFixture();
            fixture.WithGeneratedLine(
                "shader_master_label",
                "        shader_master_label " +
                "(\"Another Label\", Float) = 0");
            var locked = Track(fixture.Locked("LabelCape"));

            var result = Apply(locked, true);

            AssertRefusal(
                result,
                "A generated line does not match the expected line.",
                UnresolvableGuid);
        }

        [Test]
        public void GeneratedShaderWithCompileErrorRefuses()
        {
            var fixture = new LockedSourceFixture();
            fixture.WithGeneratedLine(
                "return tex2D",
                "                return tex2D(_MainTex, i.uv) * " +
                "_AmuseNoSuchSymbol;");
            var locked = Track(fixture.Locked("BrokenCape"));

            // No-op guard: the partner asset itself carries the compile
            // error, so the refusal cannot come from any other step.
            Assert.That(ShaderUtil.ShaderHasError(fixture.Partner),
                Is.True,
                "fixture guard: the partner must carry the compile " +
                "error");

            var result = Apply(locked, true);

            AssertRefusal(result, CompileErrorDetail, UnresolvableGuid);
            Assert.That(locked.shader, Is.SameAs(fixture.Partner),
                "a refused reconstruction must not swap the shader");
        }

        [Test]
        public void QueueAndOverrideTagsAreCarriedFromTheLockedMaterial()
        {
            var fixture = new LockedSourceFixture();
            var locked = Track(fixture.Locked("TagCape"));
            AssertGuard(fixture, locked);

            locked.renderQueue = 2450;
            locked.SetOverrideTag("RenderType", "Transparent");
            locked.SetOverrideTag("VRCFallback", "Unlit");

            var result = Apply(locked, true);

            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                result.Detail ?? "the reconstruction refused");
            Assert.That(locked.renderQueue, Is.EqualTo(2450),
                "the render queue must carry from the locked material");
            Assert.That(
                locked.GetTag("RenderType", false, null),
                Is.EqualTo("Transparent"),
                "the RenderType override tag must carry");
            Assert.That(
                locked.GetTag("VRCFallback", false, null),
                Is.EqualTo("Unlit"),
                "the VRCFallback override tag must carry");
        }

        [Test]
        public void ClonedNameStaysTheLockedName()
        {
            var fixture = new LockedSourceFixture();
            var locked = Track(fixture.Locked("Named Cape"));

            var result = Apply(locked, true);

            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                result.Detail ?? "the reconstruction refused");
            Assert.That(locked.name, Is.EqualTo("Named Cape"),
                "the reconstruction must never rename the clone");
        }

        [Test]
        public void RelockToTheSamePathInvalidatesTheMemo()
        {
            var fixture = new LockedSourceFixture("relock");
            var locked = Track(fixture.Locked("RelockCape"));

            var first = Apply(locked, true);
            Assert.That(first.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                first.Detail ?? "the first reconstruction refused");

            // The relock writes a different generated text to the same
            // asset path. The memo key binds the write time, so the test
            // pins a moved stamp and proves the invalidation on the
            // content, not on a timing race.
            var relock = new LockedSourceFixture("relock");
            relock.WithAddedDefine("AMUSE_RELOCK_MARKER");
            var regenerated = relock.Partner;
            var generatedPath = AssetDatabase.GetAssetPath(regenerated);
            Assert.That(generatedPath, Is.Not.Empty,
                "fixture guard: the regenerated partner must be an " +
                "asset");
            var stampBefore = File.GetLastWriteTimeUtc(generatedPath);
            File.SetLastWriteTimeUtc(
                generatedPath, stampBefore.AddMilliseconds(2));
            Assert.That(
                File.GetLastWriteTimeUtc(generatedPath).Ticks,
                Is.Not.EqualTo(stampBefore.Ticks),
                "fixture guard: the file system must keep the moved " +
                "stamp");
            locked.shader = regenerated;
            locked.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);

            var second = Apply(locked, true);
            Assert.That(second.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                second.Detail ?? "the relock reconstruction refused");
            Assert.That(locked.shaderKeywords,
                Does.Contain("AMUSE_RELOCK_MARKER"),
                "the relock must align the new text and carry its " +
                "keyword");
        }

        [Test]
        public void MissingOriginalNameTagRefusesWithTheNameTagDetail()
        {
            // Falsifies the tag-less reader: without reading the identity
            // tags this case would apply and return true.
            var fixture = new LockedSourceFixture();
            var locked = Track(fixture.Locked("NoNameTagCape"));
            locked.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName, null);

            var result = Apply(locked, true);

            AssertRefusal(result, NameTagMissingDetail, UnresolvableGuid);
        }

        [Test]
        public void MissingOriginalGuidTagRefusesWithTheGuidTagDetail()
        {
            // Falsifies the tag-less reader: without reading the identity
            // tags this case would apply and return true.
            var fixture = new LockedSourceFixture();
            var locked = Track(fixture.Locked("NoGuidTagCape"));
            locked.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName, null);

            var result = Apply(locked, true);

            AssertRefusal(result, GuidTagMissingDetail, UnresolvableGuid);
        }

        [Test]
        public void UnresolvableOriginalGuidRefusesWithTheResolutionDetail()
        {
            // Falsifies the tag-less reader: a reader that never resolves
            // the recorded GUID through the AssetDatabase answers true
            // here.
            var fixture = new LockedSourceFixture();
            var locked = Track(fixture.Locked("LostCape"));
            locked.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                UnresolvableGuid);

            var result = Apply(locked, true);

            AssertRefusal(
                result, GuidUnresolvableDetail, UnresolvableGuid);
        }

        [Test]
        public void UnattestedOriginalShaderRefusesWithTheAttestationDetail()
        {
            // Falsifies the tag-less reader: a reader that skips the
            // attestation seam answers true on an unattested original.
            var fixture = new LockedSourceFixture();
            var locked = Track(fixture.Locked("UnattestedCape"));

            var result = Apply(locked, false);

            AssertRefusal(
                result, AttestationFailedDetail, UnresolvableGuid);
        }

        [Test]
        public void OriginalShaderWithoutFlagPropertyRefusesWithTheFlagDetail()
        {
            // Falsifies the blind flag writer: clearing the flag without a
            // declared-property check corrupts a shader that never
            // carried it. The variant original drops the flag property
            // line, and the pair still aligns, so the refusal can only
            // come from the flag step.
            var fixture = new LockedSourceFixture();
            fixture.WithoutOriginalLine("_ShaderOptimizerEnabled (");
            var locked = Track(fixture.Locked("FlaglessCape"));
            AssertGuard(fixture, locked);

            var result = Apply(locked, true);

            AssertRefusal(
                result, FlagPropertyMissingDetail, UnresolvableGuid);
        }
    }
}
