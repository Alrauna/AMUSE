using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    internal static class AmusePlatformFinishPass
    {
        internal static void Execute(BuildContext context)
        {
            var state = PendingState(context);
            Execute(
                context,
                state,
                HostLifecycleCapability.CaptureAndEvaluate(context),
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        internal static void Execute(
            BuildContext context,
            HostLifecycleFacts facts,
            VersionConsentPresenter consentPresenter = null)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            Execute(
                context,
                PendingState(context),
                HostLifecycleCapability.Evaluate(facts),
                null,
                null,
                null,
                null,
                null,
                consentPresenter,
                null);
        }

        /// <summary>
        /// Exercises the exact lifecycle, retained-bindings, committed-graph,
        /// renderer-loop, and accounting entry while substituting only the
        /// existing public-fixture seams for family/request selection,
        /// unavailable vendor source attestation, verified frontend
        /// interpretation, and the shader-family opaque-conversion step.
        /// <para>
        /// <paramref name="poiyomiConversion"/> and
        /// <paramref name="lilToonConversion"/> are the fourth and fifth
        /// public-fixture seams. A null value means "run the real
        /// <c>PoiyomiOpaqueConversion</c> resp. <c>LilToonOpaqueTarget</c>
        /// path", which is what production does; the three other delegates
        /// keep their guards because their null is a caller defect, while
        /// these two's null is meaningful.
        /// </para>
        /// </summary>
        internal static void Execute(
            BuildContext context,
            HostLifecycleFacts facts,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics,
            VerifiedPoiyomiConversion poiyomiConversion = null,
            VerifiedLilToonConversion lilToonConversion = null,
            VersionConsentPresenter consentPresenter = null,
            Func<Material, bool> lockedOriginalAttestation = null)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (selectRequest == null)
            {
                throw new ArgumentNullException(nameof(selectRequest));
            }
            if (capturer == null) throw new ArgumentNullException(nameof(capturer));
            if (resolveSemantics == null)
            {
                throw new ArgumentNullException(nameof(resolveSemantics));
            }

            Execute(
                context,
                PendingState(context),
                HostLifecycleCapability.Evaluate(facts),
                selectRequest,
                capturer,
                resolveSemantics,
                poiyomiConversion,
                lilToonConversion,
                consentPresenter,
                lockedOriginalAttestation);
        }

        /// <summary>
        /// When the animator extension is active, returns its AnimationIndex:
        /// the same index the apply pass validates against. When it is
        /// inactive, returns false — the caller then admits swaps from the
        /// committed-graph walk alone, which is the legacy capture shape.
        /// </summary>
        private static bool TryGetAnimationIndex(
            BuildContext context, out AnimationIndex animationIndex)
        {
            try
            {
                animationIndex = context
                    .Extension<AnimatorServicesContext>().AnimationIndex;
                return true;
            }
            catch (Exception exception) when (
                IsInactiveExtensionSignal(exception))
            {
                animationIndex = null;
                return false;
            }
        }

        /// <summary>
        /// Matches the one signal NDMF 1.14.4 gives for an inactive
        /// extension. A verification on 2026-10-15 read the NDMF source and
        /// found no activity check, no optional accessor, and no dedicated
        /// exception type. The source throws a plain
        /// <c>System.Exception</c> from <c>BuildContext.Extension&lt;T&gt;()</c>
        /// with the message text below
        /// (Packages/nadena.dev.ndmf/Editor/API/BuildContext.cs). This
        /// matcher couples to that exact message text.
        ///
        /// Two drift directions exist. One: NDMF rewords the text. Then no
        /// failure matches, the exception escapes, and the build fails on
        /// the wrong arm. Two: NDMF introduces a typed exception. Then the
        /// plain-type check stops matching, and every build falls back to
        /// the legacy capture arm without an error. Re-run the source
        /// verification after each NDMF package upgrade.
        /// </summary>
        private static bool IsInactiveExtensionSignal(Exception exception)
        {
            return exception.GetType() == typeof(Exception) &&
                   string.Equals(
                       exception.Message,
                       $"Extension {typeof(AnimatorServicesContext)} not active",
                       StringComparison.Ordinal);
        }

        private static AmusePlatformFinishState PendingState(
            BuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.GetState<AmusePlatformFinishState>();
            if (state.HasExecuted)
            {
                throw new InvalidOperationException("AMUSE PlatformFinish barrier executed more than once.");
            }

            return state;
        }

        /// <summary>
        /// Test seam for headless EditMode runs. Null in production. When set,
        /// the consent gate consults it before the native dialog. Tests must
        /// answer decline through this seam; acceptance keeps flowing through
        /// the explicit presenter parameter. (H1, spec
        /// docs/superpowers/specs/2026-10-19-liltoon-2-4-0-admission-and-headless-tests-design.md)
        /// </summary>
        internal static VersionConsentPresenter PresenterForTests = null;

        /// <summary>
        /// The gate's presenter chain: an explicit presenter wins, then the
        /// test seam, then the native dialog. Factored so lightweight tests
        /// can pin that the chain reads the seam without a full build.
        /// </summary>
        internal static VersionConsentPresenter ResolveConsentPresenter(
            VersionConsentPresenter consentPresenter)
        {
            return consentPresenter ?? PresenterForTests
                ?? VersionConsentDialog.Present;
        }

        private static void Execute(
            BuildContext context,
            AmusePlatformFinishState state,
            HostLifecycleCapability lifecycle,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion,
            VersionConsentPresenter consentPresenter,
            Func<Material, bool> lockedOriginalAttestation)
        {
            state.Lifecycle = lifecycle;
            state.HasExecuted = true;
            UnityGeneratedTextureEvidence.ClearCache();
            UnityAlphaFieldEvidence.ClearCache();
            ReplacementTextureIdentity.ClearSession();
            ReplacementTextureAttestation.Versions.ClearVersionCacheForSession();
            AaoAtlasTextureAttestation.Versions.ClearVersionCacheForSession();
            Semantics.LilToon.LilToonSourceAttestation.ClearGatherMemo();
            Semantics.Poiyomi.PoiyomiSourceAttestation.ClearSourceHashMemo();
            if (!lifecycle.MayUsePositiveMutation)
            {
                // V7: a lifecycle refusal is never silent. One plain
                // English entry on the avatar root tells the user why
                // nothing happened.
                AmuseReports.LifecycleRefusal(
                    context.AvatarRootObject, lifecycle.Refusal);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    var refusalKey = AmuseReportStrings.HostKey(lifecycle.Refusal);
                    var sentence = AmuseReportStrings.Has(refusalKey)
                        ? AmuseReportStrings.Format(refusalKey)
                        : "Lifecycle refusal: " + lifecycle.Refusal;
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = sentence
                    });
                    PlayModeBuildStore.SaveOutcome(context, state);
                }
                return;
            }

            // V1 trigger: the pipeline runs only when the avatar root
            // carries the opt-in component. Absence is a total silent
            // no-op: nothing is observed and nothing is reported. This
            // check precedes the bindings invariant because an unopted
            // avatar does no work at all. A component on a child does not
            // count; the avatar root is the only switch.
            var activated = ActivatedOptimizer(context);
            if (activated == null)
            {
                return;
            }

            // Feature switch: the alpha separator runs only with its
            // toggle on. The component stays present for later sibling
            // features. Off mirrors the Disable AMUSE contract for this
            // one feature: nothing analyzed, nothing mutated, nothing
            // reported, and the consent layer below never asks.
            if (!activated.AlphaSeparatorEnabled)
            {
                return;
            }

            // Reaching positive lifecycle permission without the bindings the
            // capture pass retains is an integration defect in the caller, not a
            // domain refusal: nothing about the avatar has been observed yet.
            if (state.AnimatorBindings == null)
            {
                throw new InvalidOperationException(
                    "AMUSE PlatformFinish barrier reached positive lifecycle " +
                    "permission with no retained animator bindings.");
            }

            // The structural pass enumerated the real graph before
            // virtualization and stored it; this pass reads that stored
            // result so the checks that need real clips keep their
            // pre-virtualization view. Direct callers that bypass the pass
            // list get the enumeration inline: at that point the graph is
            // still real, so the view is equally pre-virtualization.
            if (state.StructuralGraph == null)
            {
                AmuseStructuralGraphCheck.Execute(context);
            }

            var graph = state.StructuralGraph;

            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                // Avatar scope: the exact named cause is preserved and the whole
                // avatar stops. No renderer is analyzed, so no partial result and
                // no per-renderer accounting can survive. This gate names the
                // cause exactly once, and only for avatars the gates above it
                // admitted: positive lifecycle, the opt-in component on the
                // avatar root, and the feature switch on.
                state.AvatarRefusal = graph.Refusal;
                AmuseReports.AvatarRefusal(
                    context.AvatarRootObject, graph.Refusal);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    var refusalKey = AmuseReportStrings.AvatarKey(graph.Refusal);
                    var sentence = AmuseReportStrings.Has(refusalKey)
                        ? AmuseReportStrings.Format(refusalKey)
                        : "Structural refusal: " + graph.Refusal;
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = sentence
                    });
                    PlayModeBuildStore.SaveOutcome(context, state);
                }
                return;
            }

            // D8 consent layer: one consolidated click-through per build
            // covers host versions beyond the attested maxima and shader
            // names collected from the avatar's assigned materials and
            // swap-reachable materials whose source is not a verified
            // version. The block sits after the committed-graph gate, so
            // an avatar that refuses avatar-wide is never asked. Batch
            // mode has no user to ask and refuses. Declining is a total
            // no-op after the trigger: an opted-out avatar never asks.
            var shaderTransfer = UnityMaterialSemantics
                .CollectTransferConsent(
                    AssignedAndSwapCurvedMaterials(context, graph));
            var subjects = new List<string>(lifecycle.ConsentSubjects);
            subjects.AddRange(shaderTransfer.Subjects);
            // The granted transfer names compose into every gate that
            // verifies a source identity: the transferred capture skips the
            // check for a granted name, and the conversion boundary skips it
            // for a granted name. Approval is the parallel version-keyed
            // arm (D3): any approved captured version also installs the
            // transferred capturer, which then admits members by their
            // captured package version. Both arms stay fail-closed for
            // everything else.
            // An unconsented build grants nothing and stays fail-closed.
            var grantedShaderNames =
                shaderTransfer.GrantedShaderNames.Count > 0
                    ? shaderTransfer.GrantedShaderNames
                    : null;
            // The unlock window no longer carries its own per-build
            // consent subject. V2 and V4 passed live observation on
            // 2026-09-21, so per spec section 12 the gate moved to the
            // D8 pattern: an eligible build opens the window without
            // asking. Eligibility is the attestation gate alone: a
            // locked material whose original shader passes attestation
            // opens the window.
            var windowEligible =
                TransientUnlockAvailability.WindowEligibleForConsent(
                    AssignedAndSwapCurvedMaterials(context, graph),
                    lockedOriginalAttestation);

            if (subjects.Count > 0
                && !VersionConsentDialog.ShouldProceed(
                    subjects,
                    UnityEngine.Application.isBatchMode,
                    ResolveConsentPresenter(consentPresenter)))
            {
                AmuseReports.ConsentDeclined(subjects);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = "Build consent was declined."
                    });
                    PlayModeBuildStore.SaveOutcome(context, state);
                }
                return;
            }

            if (windowEligible)
            {
                context.GetState<TransientUnlockWindowState>()
                    .ConsentGranted = true;
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 1,
                        sentence = "This avatar holds locked or transfer protected materials. A build with granted consent may convert further than this view shows."
                    });
                }
            }

            var transferShaders =
                grantedShaderNames != null ||
                shaderTransfer.AnyApprovedVersions;

            // Every avatar-scope gate has passed: the unlock window opens
            // here, before capture, so the whole pipeline below reads the
            // swapped world. The swap-in holds its own consent gate and
            // attestation gate, so an ineligible build reaches it as a
            // counted no-op.
            TransientUnlockSwapIn.SwapIn(
                context,
                context.GetState<TransientUnlockWindowState>(),
                TransientUnlockAvailability.CreateProductionRestore(),
                lockedOriginalAttestation);

            // From here the run is reported, so the apply pass may
            // summarize it with applied counts.
            state.ReachedRendererAnalysis = true;

            // While the window is open, the capture reads the world
            // through the swapped view: the stored committed graph was
            // enumerated before the swap-in and still carries every
            // observed locked original, while the slot arrays and the
            // live clips hold the clones. Folding the substitution into
            // the observed values keeps one admitted material per
            // binding, which is exactly what the apply pass validates
            // against.
            var window = context.GetState<TransientUnlockWindowState>();
            Func<Material, Material> swappedView = window.OpenPairs.Count
                > 0
                ? window.SwappedView
                : null;

            var optimizer = context.AvatarRootObject
                .GetComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var maxMipLevel = RuntimeStateResolution.ProofMipCapFrom(
                optimizer?.PreserveTransparencyMaxMipLevel ?? -1);
            var minTextureSize = RuntimeStateResolution.MinTextureSizeFrom(
                optimizer?.PreserveTransparencyMinTextureSize ?? -1);
            var minimumOpaqueCoveragePercent =
                RuntimeStateResolution.MinimumCoverageFrom(
                    optimizer?.MinimumOpaqueCoveragePercent ?? 0);
            var minimumOpaqueCoverageToPreservePercent =
                RuntimeStateResolution.MinimumPreserveCoverageFrom(
                    optimizer?.MinimumOpaqueCoverageToPreservePercent ?? 0);
            var cutoutMaxMipLevel = RuntimeStateResolution.ProofMipCapFrom(
                optimizer?.CutoutPreserveTransparencyMaxMipLevel ?? -1);
            var cutoutMinTextureSize = RuntimeStateResolution.MinTextureSizeFrom(
                optimizer?.CutoutPreserveTransparencyMinTextureSize ?? -1);
            var cutoutMinimumCoverage =
                RuntimeStateResolution.MinimumCoverageFrom(
                    optimizer?.CutoutMinimumOpaqueCoveragePercent ?? 0);
            var cutoutPreserveCoverage =
                RuntimeStateResolution.MinimumPreserveCoverageFrom(
                    optimizer?.CutoutMinimumOpaqueCoverageToPreservePercent ?? 0);
            var opaqueAlphaPercent = OpaquePercentFrom(optimizer);
            var polygonClampPercent = PolygonClampPercentFrom(optimizer);
            var densityCapPercent =
                DensityCapFromCoveragePercent(optimizer);
            var alphaPolicyBounds =
                AlphaPolicyBounds.From(opaqueAlphaPercent, polygonClampPercent);
            // The disclosure marker follows the spec's exact rule: the
            // summary names the policy only when a setting left its inert
            // default. The density alone never marks a build.
            state.AlphaPolicyActive =
                opaqueAlphaPercent < 100 || polygonClampPercent > 0;
            var ignoreOutOfRangeSlots =
                optimizer == null || optimizer.IgnoreOutOfRangeMaterialSlots;
            var allowDepthTestChange =
                optimizer == null || optimizer.AllowDepthTestChange;
            var cutoutSeparatorEnabled =
                optimizer == null || optimizer.CutoutSeparatorEnabled;
            var transparentSeparatorEnabled =
                optimizer == null || optimizer.TransparentSeparatorEnabled;
            // The separator switch facts ride the same barrier capture.
            // A switch off at the capture means its slot family never
            // separated. The apply pass reports the fact beside the
            // reach flag.
            state.CutoutSeparatorOff = !cutoutSeparatorEnabled;
            state.TransparentSeparatorOff = !transparentSeparatorEnabled;

            // The proof policy is a build-wide fact. Capture the active
            // target and the selected policy exactly once, before any
            // renderer analysis, from the build avatar's own optimizer
            // component - never from inspector state or a static
            // mutable. A target with no policy keeps the conservative
            // output value and names the fact before the analysis runs;
            // no acceptance applies to such a build.
            var samplingTarget = AmusePlatformFinishPlugin.ActiveBuildTarget();
            state.SamplingTarget = samplingTarget;
            state.HasSamplingPolicy = PlatformAlphaSamplingPolicy.TrySelect(
                samplingTarget,
                optimizer != null &&
                    optimizer.AcceptDesktopAnisotropyDifferences,
                optimizer != null &&
                    optimizer.AcceptMobileAnisotropyDifferences,
                out var samplingPolicy);
            state.SamplingPolicy = samplingPolicy;
            if (!state.HasSamplingPolicy)
            {
                AmuseReports.UnsupportedSamplingTarget(
                    context.AvatarRootObject, samplingTarget);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = "The active build target supports no sampling policy. A build on this target applies nothing. This view shows no results for it."
                    });
                }
            }

            if (samplingTarget != BuildTarget.StandaloneWindows64 && lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
            {
                state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                {
                    kind = 0,
                    sentence = "The active build target is not Windows. Texture proof needs the Windows import. This view can prove fewer triangles than a Windows build."
                });
            }

            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform, context.AvatarRootObject.transform);
                var rendererTypeName = renderer.GetType().FullName;

                var refusal = UnityRendererAlphaAnalysis.HostStructuralRefusalFor(
                    renderer,
                    out var structuralMeshSubMeshCount,
                    out var structuralMaterialSlotCount);
                if (refusal != RendererAnalysisRefusal.None)
                {
                    state.RecordRendererRefusal(refusal);
                    AmuseReports.RendererRefusal(
                        renderer,
                        refusal,
                        structuralMeshSubMeshCount,
                        structuralMaterialSlotCount);
                    if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                    {
                        RecordPlayModeRendererRefusal(
                            state, renderer, rendererPath, rendererTypeName,
                            refusal, structuralMeshSubMeshCount, structuralMaterialSlotCount, null);
                    }
                    continue;
                }

                // The live build-copy materials behind the captured admitted
                // set, index-aligned with evidence.AdmittedMaterials. Held as a
                // local transient host capability, never inside the evidence.
                IReadOnlyList<Material> admittedLiveMaterials;
                ClosedAlphaMaterialCapturer effectiveCapturer = null;
                if (transferShaders)
                {
                    var granted = shaderTransfer.GrantedShaderNames;
                    effectiveCapturer = (
                        IReadOnlyList<Material> materials,
                        IReadOnlyList<CapturedAlphaMaterialFamily> families,
                        MaterialEvidenceRequest request,
                        AlphaPolicyBounds bounds,
                        int cutoutRawMipCap,
                        int exactRawMipCap,
                        out ClosedAlphaCaptureOutcome transferred) =>
                        UnityMaterialSemantics
                            .TryCaptureClosedAlphaMaterialsTransferred(
                                materials,
                                families,
                                request,
                                bounds,
                                granted,
                                out transferred,
                                RegisteredSourceIdentity.Resolve,
                                cutoutRawMipCap,
                                exactRawMipCap,
                                // The build's effective capturer is the
                                // one site the approval admission rides
                                // (D3); every other capturer stays strict.
                                admitApprovedVersions: true);
                }

                var hasIndex = TryGetAnimationIndex(
                    context, out var animationIndex);
                CapturedAnimationEvidence evidence;
                if (hasIndex)
                {
                    evidence = UnityAnimationEvidenceCapture
                        .CaptureWithAnimationIndex(
                            rendererPath,
                            renderer.sharedMaterials,
                            graph,
                            state.AnimatorBindings,
                            animationIndex,
                            alphaPolicyBounds,
                            out admittedLiveMaterials,
                            selectRequest,
                            CaptureThroughEffectiveMaterials(
                                renderer, capturer ?? effectiveCapturer,
                                cutoutMaxMipLevel, maxMipLevel),
                            ignoreOutOfRangeSlots,
                            rendererTypeName,
                            LockedMaterialIdentity.PreCheckRefusal,
                            swappedView,
                            RegisteredSourceIdentity.Resolve,
                            cutoutRawMipCap: cutoutMaxMipLevel,
                            exactRawMipCap: maxMipLevel);
                }
                else if (selectRequest == null)
                {
                    evidence = UnityAnimationEvidenceCapture.Capture(
                        rendererPath,
                        renderer.sharedMaterials,
                        graph,
                        state.AnimatorBindings,
                        alphaPolicyBounds,
                        out admittedLiveMaterials,
                        CaptureThroughEffectiveMaterials(
                            renderer, effectiveCapturer,
                            cutoutMaxMipLevel, maxMipLevel),
                        ignoreOutOfRangeSlots,
                        rendererTypeName,
                        LockedMaterialIdentity.PreCheckRefusal,
                        swappedView,
                        RegisteredSourceIdentity.Resolve,
                        cutoutRawMipCap: cutoutMaxMipLevel,
                        exactRawMipCap: maxMipLevel);
                }
                else
                {
                    evidence = UnityAnimationEvidenceCapture
                        .CaptureGraphThroughSeams(
                            rendererPath,
                            renderer.sharedMaterials,
                            null,
                            graph,
                            state.AnimatorBindings,
                            alphaPolicyBounds,
                            selectRequest,
                            CaptureThroughEffectiveMaterials(
                                renderer, capturer,
                                cutoutMaxMipLevel, maxMipLevel),
                            out admittedLiveMaterials,
                            ignoreOutOfRangeSlots,
                            rendererTypeName,
                            LockedMaterialIdentity.PreCheckRefusal,
                            swappedView,
                            RegisteredSourceIdentity.Resolve,
                            cutoutRawMipCap: cutoutMaxMipLevel,
                            exactRawMipCap: maxMipLevel);
                }
                var effectiveResolver = resolveSemantics
                    ?? (transferShaders
                        ? (CapturedAlphaMaterialSemanticsResolver)
                            UnityMaterialSemantics.AnalyzeAlphaMaterialTransferred
                        : null);
                var blockState =
                    EffectiveMaterialMaterialization.CaptureBlockState(renderer);
                var alphaFields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                    evidence.AdmittedMaterials, maxMipLevel,
                    cutoutMaxMipLevel, minTextureSize, cutoutMinTextureSize);
                var resolved = RuntimeStateResolution.ResolveRuntimeStates(
                    rendererPath, evidence, samplingPolicy,
                    alphaFields,
                    UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                    UnityRendererAlphaAnalysis.StructuralRefusal,
                    UnityAnimationEvidenceCapture.ProofRelevantResolver,
                    UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                    UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                    effectiveResolver, maxMipLevel,
                    minTextureSize, densityCapPercent, blockState,
                    rendererTypeName: rendererTypeName);
                refusal = resolved.Refusal;
                var slots = RuntimeStateResolution.MaterialSlotsFor(
                    evidence, rendererPath,
                    UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                    rendererTypeName);
                // Every refused slot reports its own exact reason, even
                // when the renderer as a whole continues or the first
                // refusal names the renderer: one line per slot, no
                // aggregation, so a manual test can read the full list.
                // The slot entry folds the slot's texture capture facts,
                // so one refused slot is one console entry. The slots and
                // the slot results come from one MaterialSlotsFor call on
                // one evidence record, so slot i of one is slot i of the
                // other, and a divergence is an invariant defect.
                for (var slotIndex = 0;
                     slotIndex < resolved.SlotResults.Length;
                     slotIndex++)
                {
                    if (resolved.SlotResults[slotIndex].IsResolved)
                    {
                        continue;
                    }

                    var closureWay =
                        resolved.SlotResults[slotIndex].ClosureWay;
                    AmuseReports.SlotAnalysisRefusal(
                        renderer,
                        slotIndex,
                        resolved.SlotResults[slotIndex].Refusal,
                        renderer.gameObject.name,
                        resolved.SlotResults[slotIndex].Offender,
                        resolved.SlotResults[slotIndex].UnknownReason,
                        slots[slotIndex].CaptureRefusals,
                        closureWay ==
                            MaterialDependencyClosureFailure.None
                            ? null
                            : AmuseReports.ClosureFailureSentence(
                                closureWay));
                }

                // An out-of-range closure failure names a slot the renderer
                // does not have, so it has no slot result. It still reports
                // its own entry: the template takes an arbitrary slot index,
                // and the apply pass reads the same record when a split
                // could append a slot onto that index.
                foreach (var record in evidence.SlotClosureFailures)
                {
                    if (record.SlotIndex < resolved.SlotResults.Length)
                    {
                        continue;
                    }

                    AmuseReports.SlotAnalysisRefusal(
                        renderer,
                        record.SlotIndex,
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed,
                        renderer.gameObject.name,
                        closureWaySentence:
                        AmuseReports.ClosureFailureSentence(
                            record.Failure));
                }

                // A resolved slot can still carry refused material resolutions. The
                // slot stays resolved and the triangles classify Unknown; this entry
                // names the lost proof reason. Diagnostic only: no census record.
                if (resolved.Refusal == RendererAnalysisRefusal.None)
                {
                    for (var slotIndex = 0;
                         slotIndex < resolved.SlotResults.Length;
                         slotIndex++)
                    {
                        var slotResult = resolved.SlotResults[slotIndex];
                        if (!slotResult.IsResolved)
                        {
                            continue; // refused slots report through the fold loop above.
                        }
                        for (var i = 0;
                             i < slotResult.MaterialResolutions.Count;
                             i++)
                        {
                            var resolution = slotResult.MaterialResolutions[i];
                            if (resolution.IsResolved)
                            {
                                continue;
                            }
                            var offender = evidence.AdmittedMaterials[
                                slots[slotIndex].AdmittedMaterialIndices[i]];
                            AmuseReports.SlotResolutionRefused(
                                renderer, slotIndex, renderer.gameObject.name,
                                offender, resolution.Failure);
                        }
                    }
                }

                var opaqueCandidateTriangleCount = 0;
                var extractionMeshSubMeshCount = -1;
                var extractionMaterialSlotCount = -1;
                if (refusal == RendererAnalysisRefusal.None)
                {
                    var extraction = UnityRendererAlphaAnalysis.CaptureGeometry(
                        renderer, resolved.CurrentMaterials);
                    refusal = extraction.Refusal;
                    extractionMeshSubMeshCount = extraction.MeshSubMeshCount;
                    extractionMaterialSlotCount = extraction.MaterialSlotCount;
                    if (refusal == RendererAnalysisRefusal.None)
                    {
                        var plan = RuntimeStateResolution.ClassifyRuntimeStates(
                            extraction.Snapshot,
                            resolved.SlotResults,
                            UnityRendererAlphaAnalysis.TriangleClassifier,
                            UnityRendererAlphaAnalysis.OutcomeIntersector);
                        opaqueCandidateTriangleCount = plan.OpaqueTriangleCount;
                        for (var slotIndex = 0;
                             slotIndex < slots.Count;
                             slotIndex++)
                        {
                            if (resolved.SlotResults[slotIndex].IsResolved)
                            {
                                foreach (var textureRefusal in
                                         slots[slotIndex].CaptureRefusals)
                                {
                                    AmuseReports.TextureCaptureRefusal(
                                        renderer,
                                        slotIndex,
                                        textureRefusal);
                                }
                            }

                            foreach (var propertyName in
                                     slots[slotIndex].NoAlphaChannelProperties)
                            {
                                AmuseReports.TextureAlphaMissing(
                                    renderer,
                                    slotIndex,
                                    propertyName);
                            }
                        }

                        var coverageVectors =
                            SlotTransformDecision.CoverageVectorsBySlot(
                                resolved.CurrentMaterials,
                                minimumOpaqueCoveragePercent,
                                minimumOpaqueCoverageToPreservePercent,
                                cutoutMinimumCoverage,
                                cutoutPreserveCoverage);
                        var separatorOffBySlot = SlotTransformDecision
                            .SeparatorOffBySlot(
                                coverageVectors.CutoutMembershipBySlot,
                                cutoutSeparatorEnabled,
                                transparentSeparatorEnabled);
                        var prepared = RetainPreparedSeparation(
                            state,
                            extraction.MutationTarget,
                            rendererPath,
                            plan,
                            evidence,
                            admittedLiveMaterials,
                            poiyomiConversion,
                            lilToonConversion,
                            coverageVectors.CoveragePercentsBySlot,
                            coverageVectors.PreservePercentsBySlot,
                            coverageVectors.CutoutMembershipBySlot,
                            separatorOffBySlot,
                            rendererTypeName,
                            allowDepthTestChange,
                            grantedShaderNames,
                            resolved.SlotResults);

                        if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay
                            && prepared != null)
                        {
                            RecordPlayModeSlots(
                                state, renderer, rendererPath, rendererTypeName,
                                prepared.Plan, resolved.SlotResults,
                                extraction.Snapshot);
                        }
                    }
                }

                if (refusal != RendererAnalysisRefusal.None)
                {
                    state.RecordRendererRefusal(refusal);
                    AmuseReports.RendererRefusal(
                        renderer,
                        refusal,
                        extractionMeshSubMeshCount,
                        extractionMaterialSlotCount,
                        refusal ==
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed &&
                        evidence.SlotClosureFailures.Count > 0
                            ? AmuseReports.ClosureFailureSentence(
                                evidence.SlotClosureFailures[0].Failure)
                            : null);
                    if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                    {
                        var detail = refusal ==
                            RendererAnalysisRefusal
                                .MaterialDependencyClosureFailed &&
                            evidence.SlotClosureFailures.Count > 0
                                ? AmuseReports.ClosureFailureSentence(
                                    evidence.SlotClosureFailures[0].Failure)
                                : null;
                        RecordPlayModeRendererRefusal(
                            state, renderer, rendererPath, rendererTypeName,
                            refusal, extractionMeshSubMeshCount, extractionMaterialSlotCount, detail);
                    }
                    for (var slotIndex = 0;
                         slotIndex < slots.Count;
                         slotIndex++)
                    {
                        var foldedAlready =
                            resolved.SlotResults.Length > 0 &&
                            !resolved.SlotResults[slotIndex].IsResolved;
                        if (foldedAlready)
                        {
                            continue;
                        }

                        var slotEvidence = slots[slotIndex];
                        foreach (var captureRefusal in
                                 slotEvidence.CaptureRefusals)
                        {
                            AmuseReports.TextureCaptureRefusal(
                                renderer,
                                slotIndex,
                                captureRefusal);
                        }

                        foreach (var noAlphaProperty in
                                 slotEvidence.NoAlphaChannelProperties)
                        {
                            AmuseReports.TextureAlphaMissing(
                                renderer,
                                slotIndex,
                                noAlphaProperty);
                        }

                        foreach (var materialIndex in
                                 slotEvidence.AdmittedMaterialIndices)
                        {
                            var material =
                                evidence.AdmittedMaterials[materialIndex];
                            if (material.LockedIdentityRefusal !=
                                RendererAnalysisRefusal.None)
                            {
                                AmuseReports.SlotAnalysisRefusal(
                                    renderer,
                                    slotIndex,
                                    material.LockedIdentityRefusal,
                                    renderer.gameObject.name,
                                    material,
                                    unknownReason: null,
                                    captureRefusals: null,
                                    closureWaySentence: null);
                                break;
                            }
                        }
                    }

                    continue;
                }

                state.AnalyzedRendererCount++;
                state.OpaqueCandidateTriangleCount +=
                    opaqueCandidateTriangleCount;
            }
        }

        private static ClosedAlphaMaterialCapturer
            CaptureThroughEffectiveMaterials(
                Renderer renderer,
                ClosedAlphaMaterialCapturer inner,
                int cutoutRawMipCap,
                int exactRawMipCap)
        {
            if (inner == null)
            {
                inner = (
                    IReadOnlyList<Material> batchMaterials,
                    IReadOnlyList<CapturedAlphaMaterialFamily> batchFamilies,
                    MaterialEvidenceRequest batchRequest,
                    AlphaPolicyBounds batchBounds,
                    int batchCutoutRawMipCap,
                    int batchExactRawMipCap,
                    out ClosedAlphaCaptureOutcome batchOutcome) =>
                    UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                        batchMaterials, batchFamilies, batchRequest,
                        batchBounds, out batchOutcome,
                        RegisteredSourceIdentity.Resolve,
                        batchCutoutRawMipCap, batchExactRawMipCap);
            }
            return (
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                int chainCutoutRawMipCap,
                int chainExactRawMipCap,
                out ClosedAlphaCaptureOutcome transferred) =>
            {
                var effective = EffectiveMaterialMaterialization
                    .MaterializeAdmitted(renderer, materials, out var clones);
                try
                {
                    return inner(
                        effective, families,
                        request.WithRawMipCap(
                            Math.Max(cutoutRawMipCap, exactRawMipCap)),
                        bounds,
                        chainCutoutRawMipCap,
                        chainExactRawMipCap,
                        out transferred);
                }
                finally
                {
                    foreach (var clone in clones)
                    {
                        UnityEngine.Object.DestroyImmediate(clone);
                    }
                }
            };
        }

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

        internal static int OpaquePercentFrom(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 100;
            }
            return RuntimeStateResolution.OpaquePercentFrom(
                optimizer.MinimumOpaqueAlphaPercent);
        }

        internal static int PolygonClampPercentFrom(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 0;
            }
            return RuntimeStateResolution.PolygonClampPercentFrom(
                optimizer.PolygonAlphaUpperClampPercent);
        }

        internal static int DensityCapFromCoveragePercent(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 0;
            }
            return RuntimeStateResolution.DensityCapFromCoveragePercent(
                optimizer.PolygonMinimumOpaqueCoveragePercent);
        }

        private static IEnumerable<Material> AllAssignedMaterials(
            BuildContext context)
        {
            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        yield return material;
                    }
                }
            }
        }

        private static IEnumerable<Material> AssignedAndSwapCurvedMaterials(
            BuildContext context,
            CommittedControllerGraphResult graph)
        {
            var seen = new HashSet<Material>();
            foreach (var material in AllAssignedMaterials(context))
            {
                if (seen.Add(material))
                {
                    yield return material;
                }
            }
            foreach (var layer in graph.Layers)
            {
                foreach (var clip in layer.Clips)
                {
                    foreach (var binding in AnimationUtility
                                 .GetObjectReferenceCurveBindings(clip))
                    {
                        if (!binding.propertyName.StartsWith("m_Materials"))
                        {
                            continue;
                        }
                        foreach (var key in AnimationUtility
                                     .GetObjectReferenceCurve(clip, binding))
                        {
                            if (key.value is Material material
                                && seen.Add(material))
                            {
                                yield return material;
                            }
                        }
                    }
                }
            }
        }

        private static PreparedRendererSeparation RetainPreparedSeparation(
            AmusePlatformFinishState state,
            UnityRendererMutationTarget mutationTarget,
            string rendererPath,
            MeshSeparationPlan plan,
            CapturedAnimationEvidence evidence,
            IReadOnlyList<Material> admittedLiveMaterials,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion,
            IReadOnlyList<int> coveragePercentsBySlot,
            IReadOnlyList<int> preservePercentsBySlot,
            IReadOnlyList<bool> cutoutMembershipBySlot,
            bool[] separatorOffBySlot = null,
            string rendererTypeName = null,
            bool allowDepthTestChange = false,
            IReadOnlyCollection<string> grantedShaderNames = null,
            IReadOnlyList<SlotResolutionResult> slotResults = null)
        {
            var prepared = AlphaSeparationPreparation.Prepare(
                state,
                mutationTarget,
                rendererPath,
                plan,
                evidence,
                admittedLiveMaterials,
                poiyomiConversion,
                lilToonConversion,
                coveragePercentsBySlot,
                preservePercentsBySlot,
                cutoutMembershipBySlot,
                separatorOffBySlot,
                rendererTypeName,
                allowDepthTestChange,
                grantedShaderNames,
                slotResults);
            if (prepared == null)
            {
                return null;
            }

            state.Separation ??= new PreparedAlphaSeparation();
            state.Separation.Add(prepared);
            return prepared;
        }

        internal static void RecordPlayModeRendererRefusal(
            AmusePlatformFinishState state,
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            RendererAnalysisRefusal refusal,
            int meshSubMeshCount,
            int materialSlotCount,
            string detail)
        {
            var scenePath = renderer != null
                ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                    .GetReference(renderer, false)?.Path
                : null;
            var effectivePath = scenePath != null ? scenePath : rendererPath;

            var key = AmuseReportStrings.RendererKey(refusal);
            var hasCauseSentence = AmuseReportStrings.Has(key);
            var refusalSentence = hasCauseSentence
                ? AmuseReportStrings.Format(
                    key,
                    AmuseReports.SlotCountPhrase(meshSubMeshCount),
                    AmuseReports.SlotCountPhrase(materialSlotCount),
                    renderer != null ? renderer.gameObject.name : "",
                    string.IsNullOrEmpty(detail) ? "" : detail)
                : refusal.ToString();

            var slotRefusalSentence = hasCauseSentence
                ? refusalSentence
                : "Refused: AMUSE currently does not support this material";

            state.PlayModeRendererRows.Add(new SerializedTransformationRow
            {
                kind = 2,
                rendererPath = effectivePath,
                rendererTypeName = rendererTypeName,
                sentence = effectivePath + " (" + rendererTypeName + "): refused. " + refusalSentence
            });

            var materials = renderer != null ? renderer.sharedMaterials : null;
            var slotCount = materials != null ? materials.Length : (materialSlotCount > 0 ? materialSlotCount : 0);
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var mat = materials != null && slotIndex < materials.Length ? materials[slotIndex] : null;
                var sourceMat = mat != null ? RegisteredSourceIdentity.Resolve(mat) as Material ?? mat : null;
                var sourcePath = sourceMat != null ? AssetDatabase.GetAssetPath(sourceMat) : string.Empty;
                state.PlayModeRendererRows.Add(new SerializedTransformationRow
                {
                    kind = 3,
                    rendererPath = effectivePath,
                    rendererTypeName = rendererTypeName,
                    sourceMaterialPath = sourcePath,
                    slotIndex = slotIndex,
                    movedTriangles = -1,
                    provenTriangles = -1,
                    totalTriangles = -1,
                    sentence = slotRefusalSentence
                });
            }
        }

        private static int SubmeshPolygonCount(Renderer renderer, int slotIndex)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
            {
                mesh = filter.sharedMesh;
            }

            if (mesh == null || slotIndex < 0 || slotIndex >= mesh.subMeshCount)
            {
                return -1;
            }

            if (mesh.GetTopology(slotIndex) != MeshTopology.Triangles)
            {
                return -1;
            }

            return (int)(mesh.GetIndexCount(slotIndex) / 3);
        }

        internal static void RecordPlayModeSlots(
            AmusePlatformFinishState state,
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            MeshSeparationPlan plan,
            IReadOnlyList<SlotResolutionResult> slotResults,
            UnityRendererAlphaSnapshot snapshot)
        {
            var scenePath = renderer != null
                ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                    .GetReference(renderer, false)?.Path
                : null;
            var effectivePath = scenePath != null ? scenePath : rendererPath;

            var materials = renderer != null ? renderer.sharedMaterials : null;
            var submeshesByBinding = new Dictionary<int, List<SubmeshSeparationPlan>>();
            foreach (var submesh in plan.Submeshes)
            {
                if (!submeshesByBinding.TryGetValue(submesh.SourceMaterialBindingIndex, out var list))
                {
                    list = new List<SubmeshSeparationPlan>();
                    submeshesByBinding.Add(submesh.SourceMaterialBindingIndex, list);
                }
                list.Add(submesh);
            }

            var slotCount = materials != null ? materials.Length : (slotResults != null ? slotResults.Count : 0);
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var mat = materials != null && slotIndex < materials.Length ? materials[slotIndex] : null;
                var sourceMat = mat != null ? RegisteredSourceIdentity.Resolve(mat) as Material ?? mat : null;
                var sourcePath = sourceMat != null ? AssetDatabase.GetAssetPath(sourceMat) : string.Empty;

                if (slotResults != null && slotIndex < slotResults.Count && !slotResults[slotIndex].IsResolved)
                {
                    var total = SubmeshPolygonCount(renderer, slotIndex);
                    var slotKey = AmuseReportStrings.RendererKey(
                        slotResults[slotIndex].Refusal);
                    var slotSentence = AmuseReportStrings.Has(slotKey)
                        ? AmuseReportStrings.Format(
                            slotKey,
                            "",
                            "",
                            renderer != null ? renderer.gameObject.name : "",
                            "")
                        : "Refused: AMUSE currently does not support this material";
                    state.PlayModeRendererRows.Add(new SerializedTransformationRow
                    {
                        kind = 3,
                        rendererPath = effectivePath,
                        rendererTypeName = rendererTypeName,
                        sourceMaterialPath = sourcePath,
                        slotIndex = slotIndex,
                        movedTriangles = -1,
                        provenTriangles = -1,
                        totalTriangles = total,
                        sentence = slotSentence
                    });
                    continue;
                }

                if (submeshesByBinding.TryGetValue(slotIndex, out var submeshes))
                {
                    var proven = 0;
                    var total = 0;
                    var isCandidate = false;
                    foreach (var s in submeshes)
                    {
                        proven += s.OpaqueTriangleOrdinals.Count;
                        total += s.OpaqueTriangleOrdinals.Count + s.TransparentTriangleOrdinals.Count;
                        if (s.Disposition == SubmeshSeparationDisposition.WhollyOpaqueCandidate ||
                            s.Disposition == SubmeshSeparationDisposition.Split)
                        {
                            isCandidate = true;
                        }
                    }

                    string sentence;
                    if (isCandidate)
                    {
                        if (total > 0 && proven == total)
                        {
                            sentence = DryRun.RowText.ConvertableSlotSentence(total);
                        }
                        else if (proven > 0)
                        {
                            sentence = DryRun.RowText.SplittableSlotSentence(proven, total);
                        }
                        else
                        {
                            sentence = DryRun.RowText.UntouchableSlotSentence(total);
                        }
                    }
                    else
                    {
                        sentence = DryRun.RowText.UntouchableSlotSentence(total);
                    }

                    state.PlayModeRendererRows.Add(new SerializedTransformationRow
                    {
                        kind = 3,
                        rendererPath = effectivePath,
                        rendererTypeName = rendererTypeName,
                        sourceMaterialPath = sourcePath,
                        slotIndex = slotIndex,
                        movedTriangles = 0,
                        provenTriangles = proven,
                        totalTriangles = total,
                        sentence = sentence
                    });
                }
            }
        }
    }
}
