using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Alrauna.Amuse.Editor.DryRun
{
    internal enum DryRunState
    {
        Idle,
        ClassifyingBackground,
        Completed
    }

    /// <summary>
    /// The edit-time alpha separation dry run. It runs the same proof
    /// pipeline the PlatformFinish barrier runs, over the avatar in the
    /// scene, and turns the outcome into display rows. It reads. It never
    /// writes: the capture transients it creates die inside the compute
    /// scope, and the one capture route that writes project assets stays
    /// unreachable, because every capture request disallows the
    /// disk-fallback route.
    /// <para>
    /// Begin runs the avatar facts once per open. Step computes one
    /// renderer per call, behind a probe pass and the session cache. An
    /// unexpected exception logs and marks the errored display state for
    /// that fingerprint; it never becomes a refusal row, because a
    /// refusal names a fact about the avatar, not a defect.
    /// </para>
    /// <para>
    /// The two optional delegates exist for the verified fixture suites,
    /// which must stand in for vendor source attestation no stand-in
    /// shader can pass. Null keeps the production selection and the
    /// production closed capture, so the inspector call shape is the
    /// two-argument one.
    /// </para>
    /// </summary>
    internal sealed class AlphaSeparationDryRun
    {
        /// <summary>
        /// The renderer body join. The outer composite join uses the
        /// cache separator, so every body component must avoid it. The
        /// unit separator code point cannot appear in a path, a name, or
        /// a numeric field, so this join cannot collide.
        /// </summary>
        private const string BodySeparator = "\u001e";

        private readonly AmuseAvatarOptimizer _component;
        private readonly Func<BuildTarget> _buildTargetProvider;

        /// <summary>Null selects the production request selector.</summary>
        private readonly AlphaMaterialRequestSelector _selectRequest;

        /// <summary>Null selects the production closed capture.</summary>
        private readonly ClosedAlphaMaterialCapturer _capturer;

        private readonly List<MaterialTransformationRow> _avatarRows =
            new List<MaterialTransformationRow>();
        private readonly List<MaterialTransformationRow> _rendererRows =
            new List<MaterialTransformationRow>();
        private readonly HashSet<NoteKind> _notesEmitted =
            new HashSet<NoteKind>();

        private GameObject _avatarRoot;
        private CommittedControllerGraphResult _graph;
        private AlphaSamplingPolicy _samplingPolicy;
        private BuildTarget _activeTarget;
        private string _policyFingerprint;
        private string _controllerFingerprint;
        private string _graphFingerprint;
        private string _avatarKey;
        private bool _avatarCacheHit;

        // The mapped policy values the build derives once per pass. The
        // dry run derives them once per open, from the same helpers.
        private AlphaPolicyBounds _bounds;
        private int _maxMipLevel;
        private int _minTextureSize;
        private int _densityCapPercent;
        private int _minimumOpaqueCoveragePercent;
        private int _minimumOpaqueCoverageToPreservePercent;
        private int _cutoutMaxMipLevel;
        private int _cutoutMinTextureSize;
        private int _cutoutMinimumCoverage;
        private int _cutoutPreserveCoverage;
        private bool _allowDepthTestChange;

        private int _rendererIndex;
        private DryRunState _state = DryRunState.Idle;
        private CancellationTokenSource _cts;
        private Task<MeshSeparationPlan> _inFlightTask;
        private Renderer _inFlightRenderer;
        private string _inFlightRendererPath;
        private string _inFlightRendererTypeName;
        private string _inFlightRendererName;
        private IReadOnlyList<Material> _inFlightAdmittedLive;
        private IReadOnlyList<BlockStateEntry> _inFlightBlockState;
        private CapturedAnimationEvidence _inFlightEvidence;
        private RuntimeStateResolution.ResolvedRuntimeStates _inFlightResolved;
        private IReadOnlyList<CapturedMaterialSlotEvidence> _inFlightSlots;
        private string _inFlightRendererKey;
        private string _inFlightCompositeKey;
        private bool _inFlightRefreshOnly;


        /// <summary>
        /// The inspector sets this so the next draw pass advances. No-op
        /// when unset. The dry run invokes it after every advancing pass,
        /// so a serve-only pass cannot stall the sequence until the user
        /// moves the mouse.
        /// </summary>
        internal Action RepaintRequested { get; set; }

        internal bool Done { get; private set; }
        internal DryRunState State => _state;
        internal bool IsCancelled { get; private set; }
        internal int TotalRendererCount { get; private set; }
        internal int CurrentRendererIndex => _rendererIndex;

        internal IReadOnlyList<MaterialTransformationRow> AvatarRows =>
            _avatarRows;

        internal IReadOnlyList<MaterialTransformationRow> RendererRows =>
            _rendererRows;

        internal AlphaSeparationDryRun(
            AmuseAvatarOptimizer component,
            Func<BuildTarget> buildTargetProvider,
            AlphaMaterialRequestSelector selectRequest = null,
            ClosedAlphaMaterialCapturer capturer = null)
        {
            _component = component
                ?? throw new ArgumentNullException(nameof(component));
            _buildTargetProvider = buildTargetProvider
                ?? throw new ArgumentNullException(nameof(buildTargetProvider));
            _selectRequest = selectRequest;
            _capturer = capturer;
        }

        private AlphaMaterialRequestSelector EffectiveSelector =>
            _selectRequest ?? UnityMaterialSemantics.TrySelectAlphaMaterialRequests;

        // --- Begin -----------------------------------------------------------

        /// <summary>
        /// Runs the avatar facts once per open. Wrapped in the same catch
        /// shape as Step: an unexpected exception logs, adds the errored
        /// row, and stops the run.
        /// </summary>
        internal void Begin()
        {
            try
            {
                BeginBody();
            }
            catch (Exception failure)
            {
                Debug.LogException(failure);
                _avatarRows.Add(MaterialTransformationRow.Errored(
                    null, null, RowText.ErroredSentence(null, null)));
                Done = true;
                _state = DryRunState.Completed;
            }
        }

        private void BeginBody()
        {
            _avatarRoot = _component.transform.root.gameObject;
            var renderers = _avatarRoot.GetComponentsInChildren<Renderer>(true);
            TotalRendererCount = renderers.Length;
            _rendererIndex = 0;
            _state = DryRunState.Idle;
            IsCancelled = false;

            // The switches short-circuit before any enumeration, so a
            // switched-off avatar proves no renderer and costs no capture.
            if (_component.AmuseDisabled)
            {
                AddBanner(BannerKind.AmuseOff);
                Done = true;
                _state = DryRunState.Completed;
                return;
            }

            if (!_component.AlphaSeparatorEnabled)
            {
                AddBanner(BannerKind.SeparatorOff);
                Done = true;
                _state = DryRunState.Completed;
                return;
            }

            if (_component.transform != _avatarRoot.transform)
            {
                AddBanner(BannerKind.RootRequired);
                Done = true;
                _state = DryRunState.Completed;
                return;
            }

            _activeTarget = _buildTargetProvider();
            if (!PlatformAlphaSamplingPolicy.TrySelect(
                    _activeTarget,
                    _component.AcceptDesktopAnisotropyDifferences,
                    _component.AcceptMobileAnisotropyDifferences,
                    out _samplingPolicy))
            {
                AddBanner(BannerKind.NoSamplingPolicy);
                Done = true;
                _state = DryRunState.Completed;
                return;
            }

            // A non-Windows target keeps a working policy. The banner is a
            // disclosure, not a stop: the run computes rows and the user
            // reads the caveat beside them.
            if (_activeTarget != BuildTarget.StandaloneWindows64)
            {
                AddBanner(BannerKind.NonWindowsTarget);
            }

            _graph = CommittedControllerGraph.Enumerate(
                _avatarRoot, VRChatPlatformAnimatorBindings.Instance);
            if (_graph.Refusal != AvatarAnimationRefusal.None)
            {
                EmitStructuralRow();
                Done = true;
                _state = DryRunState.Completed;
                return;
            }

            if (HierarchyHasEditorOnly())
            {
                AddNoteOnce(NoteKind.EditorOnly);
            }

            MapPolicy();

            _policyFingerprint = BuildPolicyFingerprint();
            _controllerFingerprint = BuildControllerFingerprint();
            _graphFingerprint = BuildGraphFingerprint();
            _avatarKey = DryRunFingerprint.Avatar(InputsFor());

            // An avatar hit serves the stored avatar rows, which already
            // carry this session's banners and notes, because the avatar
            // fingerprint covers the target, the policy, the controllers,
            // and the graph. The renderer rows still flow through Step.
            if (DryRunCache.TryGetAvatar(_avatarKey, out var stored))
            {
                _avatarCacheHit = true;
                _avatarRows.Clear();
                _avatarRows.AddRange(stored);
                foreach (var kind in Enum.GetValues(typeof(NoteKind))
                             .Cast<NoteKind>())
                {
                    if (_avatarRows.Any(row =>
                            row.Kind == MaterialTransformationRowKind.Note &&
                            row.Sentence == RowText.NoteSentence(kind)))
                    {
                        _notesEmitted.Add(kind);
                    }
                }
            }
        }

        private void EmitStructuralRow()
        {
            var key = AmuseReportStrings.AvatarKey(_graph.Refusal);
            if (!AmuseReportStrings.Has(key))
            {
                throw new InvalidOperationException(
                    "The report string table has no sentence for key '"
                    + key + "'.");
            }

            var sentence = AmuseReportStrings.Format(key);
            _avatarRows.Add(MaterialTransformationRow.Banner(
                RowText.StructuralRowSentence(
                    StructuralPrefixFor(), sentence)));
            Done = true;
            _state = DryRunState.Completed;
        }

        /// <summary>
        /// Display attribution only: the refusing graph exposes no layers,
        /// so this bounded scan names the one renderer path the
        /// event-carrying clips animate, when exactly one path appears.
        /// It never feeds proof.
        /// </summary>
        private string StructuralPrefixFor()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<Object>();
            foreach (var entry in VRChatPlatformAnimatorBindings.Instance
                         .GetInnateControllers(_avatarRoot))
            {
                if (!(entry.Item2 is AnimatorController controller) ||
                    !seen.Add(controller))
                {
                    continue;
                }

                foreach (var layer in controller.layers)
                {
                    var machine = layer.stateMachine;
                    if (machine == null)
                    {
                        continue;
                    }

                    foreach (var state in machine.states)
                    {
                        if (!(state.state.motion is AnimationClip clip) ||
                            clip.events.Length == 0)
                        {
                            continue;
                        }

                        foreach (var binding in AnimationUtility
                                     .GetCurveBindings(clip)
                                     .Select(item => item.path)
                                     .Concat(AnimationUtility
                                         .GetObjectReferenceCurveBindings(clip)
                                         .Select(item => item.path)))
                        {
                            if (binding.Length > 0)
                            {
                                paths.Add(binding);
                            }
                        }
                    }
                }
            }

            return paths.Count == 1
                ? "'" + paths.First() + "' in '" + _avatarRoot.name + "': "
                : "";
        }

        private void MapPolicy()
        {
            _maxMipLevel = RuntimeStateResolution.ProofMipCapFrom(
                _component.PreserveTransparencyMaxMipLevel);
            _minTextureSize = RuntimeStateResolution.MinTextureSizeFrom(
                _component.PreserveTransparencyMinTextureSize);
            _densityCapPercent =
                RuntimeStateResolution.DensityCapFromCoveragePercent(
                    _component.PolygonMinimumOpaqueCoveragePercent);
            _minimumOpaqueCoveragePercent =
                RuntimeStateResolution.MinimumCoverageFrom(
                    _component.MinimumOpaqueCoveragePercent);
            _minimumOpaqueCoverageToPreservePercent =
                RuntimeStateResolution.MinimumPreserveCoverageFrom(
                    _component.MinimumOpaqueCoverageToPreservePercent);
            _cutoutMaxMipLevel = RuntimeStateResolution.ProofMipCapFrom(
                _component.CutoutPreserveTransparencyMaxMipLevel);
            _cutoutMinTextureSize = RuntimeStateResolution.MinTextureSizeFrom(
                _component.CutoutPreserveTransparencyMinTextureSize);
            _cutoutMinimumCoverage =
                RuntimeStateResolution.MinimumCoverageFrom(
                    _component.CutoutMinimumOpaqueCoveragePercent);
            _cutoutPreserveCoverage =
                RuntimeStateResolution.MinimumPreserveCoverageFrom(
                    _component.CutoutMinimumOpaqueCoverageToPreservePercent);
            _allowDepthTestChange = _component.AllowDepthTestChange;
            _bounds = AlphaPolicyBounds.From(
                RuntimeStateResolution.OpaquePercentFrom(
                    _component.MinimumOpaqueAlphaPercent),
                RuntimeStateResolution.PolygonClampPercentFrom(
                    _component.PolygonAlphaUpperClampPercent));
        }

        private bool HierarchyHasEditorOnly()
        {
            foreach (var transform in
                     _avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                if (transform.CompareTag("EditorOnly"))
                {
                    return true;
                }
            }

            return false;
        }

        // --- Step ------------------------------------------------------------

        /// <summary>
        /// Advances classification by one unit of work. When synchronous
        /// is true, completes one renderer in full before returning.
        /// When synchronous is false, transitions to background classification
        /// and returns immediately to keep the main thread responsive.
        /// </summary>
        internal bool Step(bool synchronous = true)
        {
            if (Done || _avatarRoot == null || _state == DryRunState.Completed)
            {
                _state = DryRunState.Completed;
                Done = true;
                return false;
            }

            if (_state == DryRunState.Idle)
            {
                if (!StepIdle(synchronous))
                {
                    return false;
                }

                if (_state != DryRunState.ClassifyingBackground)
                {
                    return true;
                }

                if (!synchronous)
                {
                    return true;
                }
            }

            if (_state == DryRunState.ClassifyingBackground)
            {
                return StepClassifyingBackground(synchronous);
            }

            return false;
        }

        /// <summary>
        /// Advances every remaining step until the run completes.
        /// </summary>
        internal void PumpAll()
        {
            while (!Done && Step(synchronous: true))
            {
            }
        }

        internal void Cancel(int timeoutMs = 0)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The completion continuation disposed the source after
                    // the task finished but before teardown reached Cancel.
                }
            }

            if (timeoutMs > 0 && _inFlightTask != null && !_inFlightTask.IsCompleted)
            {
                try
                {
                    _inFlightTask.Wait(timeoutMs);
                }
                catch (Exception)
                {
                    // Suppress AggregateException and OperationCanceledException during shutdown wait.
                }
            }

            ClearInFlight();
            _state = DryRunState.Completed;
            Done = true;
            IsCancelled = true;
            RepaintRequested?.Invoke();
        }

        private void ClearInFlight()
        {
            if (_inFlightTask == null || _inFlightTask.IsCompleted)
            {
                _cts?.Dispose();
            }

            _cts = null;
            _inFlightTask = null;
            _inFlightRenderer = null;
            _inFlightRendererPath = null;
            _inFlightRendererTypeName = null;
            _inFlightRendererName = null;
            _inFlightAdmittedLive = null;
            _inFlightBlockState = null;
            _inFlightEvidence = null;
            _inFlightResolved = null;
            _inFlightSlots = null;
            _inFlightRendererKey = null;
            _inFlightCompositeKey = null;
            _inFlightRefreshOnly = false;
        }

        private void CompleteRun()
        {
            if (_rendererRows.Count == 0 && !_avatarCacheHit &&
                !HierarchyHasRenderer())
            {
                _avatarRows.Add(MaterialTransformationRow.Note(
                    RowText.NoRenderersSentence()));
            }

            if (_avatarKey != null && !_avatarCacheHit)
            {
                DryRunCache.StoreAvatar(_avatarKey, _avatarRows.ToArray());
            }

            Done = true;
            _state = DryRunState.Completed;
            RepaintRequested?.Invoke();
        }

        private bool HierarchyHasRenderer()
        {
            return _avatarRoot.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer != null);
        }

        private bool StepIdle(bool synchronous)
        {
            var renderers = _avatarRoot.GetComponentsInChildren<Renderer>(true);
            if (_rendererIndex >= renderers.Length)
            {
                CompleteRun();
                return false;
            }

            var renderer = renderers[_rendererIndex];
            if (renderer == null)
            {
                // Unity overloaded equality reports a destroyed object.
                _rendererIndex++;
                return true;
            }

            var rendererPath = AnimationUtility.CalculateTransformPath(
                renderer.transform, _avatarRoot.transform);
            var rendererTypeName = renderer.GetType().FullName;

            // The probe pass enumerates the admitted set and the texture
            // facts with no capture, so the composite key can be built
            // before any GPU work.
            var refreshOnly = Probe(
                renderer, rendererPath, rendererTypeName, out var admittedLive);
            var body = BuildRendererBody(
                renderer, rendererPath, rendererTypeName, admittedLive);
            var rendererKey = body;
            var composite = DryRunFingerprint.Composite(_avatarKey, rendererKey);

            if (refreshOnly)
            {
                AddNoteOnce(NoteKind.RefreshOnly);
            }
            else
            {
                if (DryRunCache.TryGetRenderer(composite, out var stored))
                {
                    _rendererRows.AddRange(stored);
                    _rendererIndex++;
                    RepaintRequested?.Invoke();
                    return true;
                }

                if (DryRunCache.IsErrored(composite))
                {
                    _rendererRows.Add(MaterialTransformationRow.Errored(
                        rendererPath,
                        rendererTypeName,
                        RowText.ErroredSentence(
                            rendererPath, rendererTypeName)));
                    _rendererIndex++;
                    RepaintRequested?.Invoke();
                    return true;
                }
            }

            IReadOnlyList<BlockStateEntry> blockState;
            CapturedAnimationEvidence evidence;
            RuntimeStateResolution.ResolvedRuntimeStates resolved;
            IReadOnlyList<CapturedMaterialSlotEvidence> slots;
            UnityRendererAlphaExtraction extraction;

            try
            {
                blockState =
                    EffectiveMaterialMaterialization.CaptureBlockState(renderer);
                evidence = CaptureFor(
                    rendererPath,
                    renderer.sharedMaterials,
                    RealCapturer(renderer),
                    rendererTypeName,
                    out _);

                RecordConsentFacts(evidence, admittedLive);

                var alphaFields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                    evidence.AdmittedMaterials, _maxMipLevel,
                    _cutoutMaxMipLevel, _minTextureSize,
                    _cutoutMinTextureSize);
                resolved = RuntimeStateResolution.ResolveRuntimeStates(
                    rendererPath,
                    evidence,
                    _samplingPolicy,
                    alphaFields,
                    UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                    UnityRendererAlphaAnalysis.StructuralRefusal,
                    UnityAnimationEvidenceCapture.ProofRelevantResolver,
                    UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                    UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                    UnityMaterialSemantics.AnalyzeAlphaMaterialTransferred,
                    _maxMipLevel,
                    _minTextureSize,
                    _densityCapPercent,
                    blockState,
                    rendererTypeName);
                slots = RuntimeStateResolution.MaterialSlotsFor(
                    evidence, rendererPath,
                    UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                    rendererTypeName);

                if (resolved.Refusal != RendererAnalysisRefusal.None)
                {
                    var refusalRows = DryRunRowBuilder.BuildResolvedRefusalRows(
                        renderer, rendererPath, rendererTypeName, resolved, slots);
                    _rendererRows.AddRange(refusalRows);
                    if (!refreshOnly && _avatarKey != null)
                    {
                        DryRunCache.StoreRenderer(_avatarKey, rendererKey, refusalRows);
                    }

                    _rendererIndex++;
                    RepaintRequested?.Invoke();
                    return true;
                }

                extraction = UnityRendererAlphaAnalysis.CaptureGeometry(
                    renderer, resolved.CurrentMaterials);
                if (extraction.Refusal != RendererAnalysisRefusal.None)
                {
                    var refusalRows = new[]
                    {
                        DryRunRowBuilder.RendererRefusedRow(
                            rendererPath, rendererTypeName, extraction.Refusal,
                            extraction.MeshSubMeshCount,
                            extraction.MaterialSlotCount,
                            renderer.gameObject.name)
                    };
                    _rendererRows.AddRange(refusalRows);
                    if (!refreshOnly && _avatarKey != null)
                    {
                        DryRunCache.StoreRenderer(_avatarKey, rendererKey, refusalRows);
                    }

                    _rendererIndex++;
                    RepaintRequested?.Invoke();
                    return true;
                }
            }
            catch (Exception failure)
            {
                Debug.LogException(failure);
                DryRunCache.MarkErrored(composite);
                _rendererRows.Add(MaterialTransformationRow.Errored(
                    rendererPath,
                    rendererTypeName,
                    RowText.ErroredSentence(
                        rendererPath, rendererTypeName)));
                _rendererIndex++;
                RepaintRequested?.Invoke();
                return true;
            }

            _cts = new CancellationTokenSource();
            var source = _cts;
            var token = source.Token;
            var snapshot = extraction.Snapshot;
            var slotResults = resolved.SlotResults;

            _inFlightRenderer = renderer;
            _inFlightRendererPath = rendererPath;
            _inFlightRendererTypeName = rendererTypeName;
            _inFlightRendererName = renderer.gameObject.name;
            _inFlightAdmittedLive = admittedLive;
            _inFlightBlockState = blockState;
            _inFlightEvidence = evidence;
            _inFlightResolved = resolved;
            _inFlightSlots = slots;
            _inFlightRendererKey = rendererKey;
            _inFlightCompositeKey = composite;
            _inFlightRefreshOnly = refreshOnly;

            _inFlightTask = Task.Run(
                () => RuntimeStateResolution.ClassifyRuntimeStates(
                    snapshot, slotResults,
                    UnityRendererAlphaAnalysis.TriangleClassifier,
                    UnityRendererAlphaAnalysis.OutcomeIntersector,
                    token),
                token);
            // Side continuation owning disposal: it runs exactly when the
            // task completes, even if cancellation abandoned the task and
            // ClearInFlight never saw a completed task to dispose for. The
            // source is captured in a local so a later run's source can
            // never be disposed by this continuation. ClearInFlight's
            // completed-path dispose stays; Dispose is idempotent.
            _inFlightTask.ContinueWith(
                _ => source.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            _state = DryRunState.ClassifyingBackground;

            return true;
        }

        private bool StepClassifyingBackground(bool synchronous)
        {
            if (synchronous && !_inFlightTask.IsCompleted)
            {
                try
                {
                    _inFlightTask.Wait();
                }
                catch (Exception)
                {
                    // Suppress AggregateException and OperationCanceledException during synchronous wait.
                }
            }

            if (!_inFlightTask.IsCompleted)
            {
                return true;
            }

            if (_inFlightTask.IsCanceled)
            {
                ClearInFlight();
                _state = DryRunState.Completed;
                Done = true;
                IsCancelled = true;
                return false;
            }

            if (_inFlightTask.IsFaulted)
            {
                var exception = _inFlightTask.Exception?.InnerException ?? _inFlightTask.Exception;
                Debug.LogException(exception);
                DryRunCache.MarkErrored(_inFlightCompositeKey);
                _rendererRows.Add(MaterialTransformationRow.Errored(
                    _inFlightRendererPath,
                    _inFlightRendererTypeName,
                    RowText.ErroredSentence(
                        _inFlightRendererPath, _inFlightRendererTypeName)));
                ClearInFlight();
                _rendererIndex++;
                _state = DryRunState.Idle;
                RepaintRequested?.Invoke();
                return true;
            }

            try
            {
                var plan = _inFlightTask.Result;
                var alreadyOpaqueBySlot = DeriveAlreadyOpaqueBySlot(
                    _inFlightResolved,
                    _inFlightEvidence,
                    _inFlightAdmittedLive,
                    _inFlightBlockState,
                    _inFlightRendererPath,
                    _inFlightRendererTypeName,
                    _allowDepthTestChange);

                // One coverage pair per slot, selected by the slot's own
                // recorded proof arm through the same builder the build
                // path reads, so the dry run can never show a verdict the
                // build would not prepare.
                var coverageVectors =
                    SlotTransformDecision.CoverageVectorsBySlot(
                        _inFlightResolved.CurrentMaterials,
                        _minimumOpaqueCoveragePercent,
                        _minimumOpaqueCoverageToPreservePercent,
                        _cutoutMinimumCoverage,
                        _cutoutPreserveCoverage);
                var verdicts = SlotTransformDecision.Decide(
                    plan,
                    _inFlightResolved.SlotResults,
                    alreadyOpaqueBySlot,
                    coverageVectors.CoveragePercentsBySlot,
                    coverageVectors.PreservePercentsBySlot);
                var rows = DryRunRowBuilder.BuildRows(
                    _inFlightRenderer,
                    _inFlightRendererPath,
                    _inFlightRendererTypeName,
                    verdicts,
                    _inFlightResolved.SlotResults,
                    _inFlightSlots,
                    _inFlightRendererName);

                _rendererRows.AddRange(rows);
                if (!_inFlightRefreshOnly && _avatarKey != null)
                {
                    DryRunCache.StoreRenderer(_avatarKey, _inFlightRendererKey, rows);
                }
            }
            catch (Exception failure)
            {
                Debug.LogException(failure);
                DryRunCache.MarkErrored(_inFlightCompositeKey);
                _rendererRows.Add(MaterialTransformationRow.Errored(
                    _inFlightRendererPath,
                    _inFlightRendererTypeName,
                    RowText.ErroredSentence(
                        _inFlightRendererPath, _inFlightRendererTypeName)));
            }
            finally
            {
                ClearInFlight();
                _rendererIndex++;
                _state = DryRunState.Idle;
                RepaintRequested?.Invoke();
            }

            return true;
        }

        // --- Capture wiring ---------------------------------------------------

        private CapturedAnimationEvidence CaptureFor(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            ClosedAlphaMaterialCapturer capturer,
            string rendererTypeName,
            out IReadOnlyList<Material> admittedLive)
        {
            if (_selectRequest == null)
            {
                return UnityAnimationEvidenceCapture.Capture(
                    rendererPath,
                    currentSlots,
                    _graph,
                    VRChatPlatformAnimatorBindings.Instance,
                    _bounds,
                    out admittedLive,
                    capturer,
                    _component.IgnoreOutOfRangeMaterialSlots,
                    rendererTypeName,
                    LockedMaterialIdentity.PreCheckRefusal,
                    null,
                    null,
                    cutoutRawMipCap: _cutoutMaxMipLevel,
                    exactRawMipCap: _maxMipLevel);
            }

            return UnityAnimationEvidenceCapture.CaptureGraphThroughSeams(
                rendererPath,
                currentSlots,
                null,
                _graph,
                VRChatPlatformAnimatorBindings.Instance,
                _bounds,
                _selectRequest,
                capturer,
                out admittedLive,
                _component.IgnoreOutOfRangeMaterialSlots,
                rendererTypeName,
                LockedMaterialIdentity.PreCheckRefusal,
                null,
                null,
                cutoutRawMipCap: _cutoutMaxMipLevel,
                exactRawMipCap: _maxMipLevel);
        }

        /// <summary>
        /// The cheap probe: one capture over a no-op capturer that admits
        /// nothing, so the admitted set and the texture facts enumerate
        /// without any GPU work. The texture facts re-derive every open
        /// here, so a run served from the renderer cache still carries its
        /// streaming and live-texture disclosures. Also reports whether a
        /// live RenderTexture rides the admitted set, which forces the
        /// full compute path and withholds the cache store for this
        /// renderer.
        /// </summary>
        private bool Probe(
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            out IReadOnlyList<Material> admittedLive)
        {
            CaptureFor(
                rendererPath,
                renderer.sharedMaterials,
                ProbeCapturer,
                rendererTypeName,
                out admittedLive);

            var refreshOnly = false;
            foreach (var material in admittedLive)
            {
                if (material == null ||
                    !EffectiveSelector(
                        material, out _, out var alphaRequest, out _))
                {
                    continue;
                }

                foreach (var textureProperty in alphaRequest.TextureProperties)
                {
                    var texture = material.GetTexture(
                        textureProperty.PropertyName);
                    if (texture is RenderTexture)
                    {
                        refreshOnly = true;
                    }
                    else if (IsStreamingMipmap(texture))
                    {
                        if (!CanDecodeInMemory(texture))
                        {
                            AddNoteOnce(NoteKind.StreamingMipmaps);
                        }
                    }
                }

                // The locked half of the consent fact re-derives on the
                // probe set with the same read the capture applies, so a
                // renderer served from the cache keeps its disclosure.
                // The transfer-subject half stays compute-only, because
                // only the capture runs attestation; a cache-served run
                // can lose that narrower fact, and the report records it.
                if (LockedMaterialIdentity.PreCheckRefusal(material) !=
                    RendererAnalysisRefusal.None)
                {
                    AddNoteOnce(NoteKind.LockedMaterials);
                }
            }

            return refreshOnly;
        }

        private static readonly ClosedAlphaMaterialCapturer ProbeCapturer =
            (
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                int cutoutRawMipCap,
                int exactRawMipCap,
                out ClosedAlphaCaptureOutcome outcome) =>
            {
                var ordinals = new List<int>(materials.Count);
                for (var index = 0; index < materials.Count; index++)
                {
                    ordinals.Add(index);
                }

                outcome = new ClosedAlphaCaptureOutcome(
                    Array.Empty<CapturedAlphaMaterial>(), ordinals);
                return true;
            };

        /// <summary>
        /// Materializes each admitted batch against property blocks.
        /// Transients die in the finally block.
        /// The capture request disallows the disk-fallback route, so no
        /// capture creates assets on disk.
        /// </summary>
        private ClosedAlphaMaterialCapturer RealCapturer(Renderer renderer)
        {
            var inner = _capturer ?? DefaultInnerCapturer();
            return (
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                int chainCutoutRawMipCap,
                int chainExactRawMipCap,
                out ClosedAlphaCaptureOutcome outcome) =>
            {
                var effective = EffectiveMaterialMaterialization
                    .MaterializeAdmitted(renderer, materials, out var clones);
                try
                {
                    // The no-asset-writes guarantee rides the capture
                    // request: the request withdraws the streaming route's
                    // disk-fallback clone, so no capture in this batch can
                    // create assets on disk. The optimizer's raw policy mip
                    // caps ride the same request: the request carries the
                    // wider of the two arms, so the unshared fallback
                    // over-captures, and the two arm values ride beside it
                    // so the closed capture can pin each bucket to its arm.
                    return inner(
                        effective,
                        families,
                        request.DisallowingDiskFallback()
                            .WithRawMipCap(Math.Max(
                                _cutoutMaxMipLevel, _maxMipLevel)),
                        bounds,
                        chainCutoutRawMipCap,
                        chainExactRawMipCap,
                        out outcome);
                }
                finally
                {
                    foreach (var clone in clones)
                    {
                        Object.DestroyImmediate(clone);
                    }
                }
            };
        }

        private static ClosedAlphaMaterialCapturer DefaultInnerCapturer()
        {
            return (
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                int cutoutRawMipCap,
                int exactRawMipCap,
                out ClosedAlphaCaptureOutcome outcome) =>
                UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                    materials, families, request, bounds, out outcome, null,
                    cutoutRawMipCap, exactRawMipCap);
        }

        /// <summary>
        /// Checks whether a streaming texture can decode in memory from disk.
        /// Only authoring PNG files decode directly. ImageConversion.LoadImage
        /// decodes PNG and JPG bytes only. A TGA source rides the
        /// readable-clone route, which this preview disallows.
        /// </summary>
        private static bool CanDecodeInMemory(Texture texture)
        {
            if (!(texture is Texture2D))
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            if (!SourceImageAlphaReader.AdmitsExtension(
                    Path.GetExtension(path).ToLowerInvariant()))
            {
                return false;
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.alphaSource == TextureImporterAlphaSource.FromGrayScale)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Counts triangles for one submesh safely from the shared mesh.
        /// Returns negative one when the submesh or topology is invalid.
        /// </summary>
        internal static int SubmeshPolygonCount(Renderer renderer, int slotIndex)
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

        /// <summary>
        /// Checks whether an unresolved slot evaluates at build time.
        /// Attested locked materials and streaming textures evaluate at build time.
        /// </summary>
        internal static bool IsSlotDeferred(
            Renderer renderer,
            int slotIndex,
            SlotResolutionResult slotResult,
            CapturedMaterialSlotEvidence slotEvidence)
        {
            if (renderer == null || renderer.sharedMaterials == null)
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= renderer.sharedMaterials.Length)
            {
                return false;
            }

            var material = renderer.sharedMaterials[slotIndex];
            if (material == null)
            {
                return false;
            }

            if (LockedMaterialIdentity.RecognizedLockedIdentity(material) &&
                (LockedMaterialIdentity.OriginalShaderAttested(material) ||
                 IsAttestedOriginalShader(material)))
            {
                return true;
            }

            if (HasUnreadableAlphaStreamingTexture(material))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the original shader tag names a supported shader.
        /// </summary>
        private static bool IsAttestedOriginalShader(Material material)
        {
            var tag = material.GetTag(
                LockedMaterialIdentity.OriginalShaderTagName, true, null);
            return tag == PoiyomiMaterialSemantics.PoiyomiToonShaderName ||
                   tag == PoiyomiMaterialSemantics.PoiyomiTwoPassShaderName;
        }

        private static bool HasUnreadableAlphaStreamingTexture(Material material)
        {
            if (material == null ||
                !UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    material, out _, out var alphaRequest, out _))
            {
                return false;
            }

            foreach (var textureProperty in alphaRequest.TextureProperties)
            {
                var tex = material.GetTexture(textureProperty.PropertyName);
                if (IsStreamingMipmap(tex) && !CanDecodeInMemory(tex))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStreamingMipmap(Texture texture)
        {
            if (!(texture is Texture2D))
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.streamingMipmaps;
        }

        // --- Resolution and verdicts ------------------------------------------

        /// <summary>
        /// A locked or transfer protected material reached the captured
        /// set: a locked identity names its refusal on the record, and a
        /// material the selector admits but the closed capture did not
        /// attest is a consent subject. Both facts surface once as the
        /// consent note row. A batch member the streaming intercept
        /// skipped is neither: its sentinel is the streaming skip, and
        /// the streaming note is its sole disclosure.
        /// </summary>
        private void RecordConsentFacts(
            CapturedAnimationEvidence evidence,
            IReadOnlyList<Material> admittedLive)
        {
            for (var index = 0; index < evidence.AdmittedMaterials.Count;
                 index++)
            {
                var captured = evidence.AdmittedMaterials[index];
                if (captured.LockedIdentityRefusal !=
                    RendererAnalysisRefusal.None)
                {
                    AddNoteOnce(NoteKind.LockedMaterials);
                    continue;
                }

                if (captured.Family == CapturedAlphaMaterialFamily.Unsupported
                    && index < admittedLive.Count &&
                    EffectiveSelector(
                        admittedLive[index],
                        out _,
                        out _,
                        out _))
                {
                    if (HasUnreadableAlphaStreamingTexture(admittedLive[index]))
                    {
                        AddNoteOnce(NoteKind.StreamingMipmaps);
                    }
                    else
                    {
                        AddNoteOnce(NoteKind.LockedMaterials);
                    }
                }
            }
        }

        /// <summary>
        /// The per-slot already-opaque flags, read only, through the same
        /// per-family eligibility entries the conversion path consults:
        /// the opaque lilToon frontend maps to itself, and a Poiyomi
        /// material classifies AlreadyOpaque when its effective render
        /// state carries every canonical opacity fact. Every other family
        /// keeps the verdict ladder's own answer.
        /// </summary>
        private static bool[] DeriveAlreadyOpaqueBySlot(
            RuntimeStateResolution.ResolvedRuntimeStates resolved,
            CapturedAnimationEvidence evidence,
            IReadOnlyList<Material> admittedLive,
            IReadOnlyList<BlockStateEntry> blockState,
            string rendererPath,
            string rendererTypeName,
            bool allowDepthTestChange)
        {
            var flags = new bool[resolved.CurrentMaterials.Count];
            var conversionBindings = ConversionBindingsFor(
                evidence,
                rendererPath,
                PoiyomiOpaqueConversion.ConversionEvidenceRequest,
                rendererTypeName);
            for (var slot = 0; slot < flags.Length; slot++)
            {
                var captured = resolved.CurrentMaterials[slot];
                if (captured == null)
                {
                    continue;
                }

                if (captured.Family == CapturedAlphaMaterialFamily.LilToon)
                {
                    flags[slot] = true;
                    continue;
                }

                if (captured.Family != CapturedAlphaMaterialFamily.Poiyomi)
                {
                    continue;
                }

                var liveIndex = slot < evidence.CurrentMaterialIndices.Count
                    ? evidence.CurrentMaterialIndices[slot]
                    : -1;
                var live = liveIndex >= 0 && liveIndex < admittedLive.Count
                    ? admittedLive[liveIndex]
                    : null;
                if (live == null)
                {
                    continue;
                }

                if (!AnimatedPropertyAdmission.TryAdmitDerivedEvidence(
                        captured,
                        conversionBindings,
                        PoiyomiOpaqueConversion.ConversionEvidenceRequest,
                        out var derived,
                        out _,
                        UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                        RuntimeStateResolution.FilterBlockEntriesForSlot(
                            blockState, slot),
                        evidence.AdmittedMaterials))
                {
                    continue;
                }

                EffectiveRenderState.ReadEffectiveRenderState(
                    live, out var queue, out var renderType);
                flags[slot] =
                    PoiyomiOpaqueConversion.EvaluateVerifiedEligibility(
                        derived, queue, renderType, allowDepthTestChange)
                        .Outcome ==
                    PoiyomiOpaqueConversionOutcome.AlreadyOpaque;
            }

            return flags;
        }

        private static IReadOnlyList<(CapturedFloatBinding Binding,
            AnimatedPropertyRef Reference)> ConversionBindingsFor(
            CapturedAnimationEvidence evidence,
            string rendererPath,
            MaterialEvidenceRequest relevance,
            string rendererTypeName)
        {
            var bindings =
                new List<(CapturedFloatBinding, AnimatedPropertyRef)>();
            foreach (var clip in evidence.Clips)
            {
                foreach (var binding in clip.FloatBindings)
                {
                    if (UnityAnimationEvidenceCapture.ResolveProofRelevant(
                            binding,
                            rendererPath,
                            relevance,
                            out var reference,
                            rendererTypeName) ==
                        ProofRelevantBindingResolution.RendererWide)
                    {
                        bindings.Add((binding, reference));
                    }
                }
            }

            return bindings;
        }

        private void AddBanner(BannerKind kind)
        {
            _avatarRows.Add(MaterialTransformationRow.Banner(
                RowText.BannerSentence(kind)));
        }

        private void AddNoteOnce(NoteKind kind)
        {
            if (!_notesEmitted.Add(kind))
            {
                return;
            }

            _avatarRows.Add(MaterialTransformationRow.Note(
                RowText.NoteSentence(kind)));
        }

        // --- Fingerprints -------------------------------------------------------

        private DryRunFingerprintInputs InputsFor()
        {
            return new DryRunFingerprintInputs(
                _avatarRoot.GetInstanceID(),
                _activeTarget,
                _policyFingerprint,
                _controllerFingerprint,
                _graphFingerprint);
        }

        private string BuildPolicyFingerprint()
        {
            var values = new object[]
            {
                _component.AmuseDisabled,
                _component.AlphaSeparatorEnabled,
                _component.PreserveTransparencyMaxMipLevel,
                _component.PreserveTransparencyMinTextureSize,
                _component.MinimumOpaqueCoveragePercent,
                _component.MinimumOpaqueCoverageToPreservePercent,
                _component.CutoutPreserveTransparencyMaxMipLevel,
                _component.CutoutPreserveTransparencyMinTextureSize,
                _component.CutoutMinimumOpaqueCoveragePercent,
                _component.CutoutMinimumOpaqueCoverageToPreservePercent,
                _component.MinimumOpaqueAlphaPercent,
                _component.PolygonAlphaUpperClampPercent,
                _component.PolygonMinimumOpaqueCoveragePercent,
                _component.AcceptDesktopAnisotropyDifferences,
                _component.AcceptMobileAnisotropyDifferences,
                _component.IgnoreOutOfRangeMaterialSlots,
                _component.AllowDepthTestChange
            };
            return string.Join(
                BodySeparator,
                values.Select(value =>
                    value is bool flag
                        ? (flag ? "1" : "0")
                        : Convert.ToString(value, CultureInfo.InvariantCulture)));
        }

        private string BuildControllerFingerprint()
        {
            var parts = new List<string>();
            var seen = new HashSet<Object>();
            foreach (var entry in VRChatPlatformAnimatorBindings.Instance
                         .GetInnateControllers(_avatarRoot))
            {
                if (entry.Item2 == null || !seen.Add(entry.Item2))
                {
                    continue;
                }

                parts.Add(AssetIdentity(entry.Item2));
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join(BodySeparator, parts);
        }

        private string BuildGraphFingerprint()
        {
            var parts = new List<string> { _graph.Refusal.ToString() };
            foreach (var layer in _graph.Layers)
            {
                parts.Add(layer.ControllerName
                    + ":" + layer.LayerIndex.ToString(
                        CultureInfo.InvariantCulture)
                    + ":" + layer.Clips.Count.ToString(
                        CultureInfo.InvariantCulture));
            }

            return string.Join(BodySeparator, parts);
        }

        private static Mesh RendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            // A MeshRenderer draws the mesh of its sibling MeshFilter.
            return renderer is MeshRenderer
                ? renderer.GetComponent<MeshFilter>()?.sharedMesh
                : null;
        }

        private string BuildRendererBody(
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            IReadOnlyList<Material> admittedLive)
        {
            var mesh = RendererMesh(renderer);
            var parts = new List<string>
            {
                rendererPath,
                rendererTypeName,
                mesh != null ? AssetIdentity(mesh) : "null"
            };
            foreach (var material in renderer.sharedMaterials)
            {
                parts.Add(AssetIdentity(material));
            }

            foreach (var material in admittedLive)
            {
                parts.Add(AssetIdentity(material));
            }

            foreach (var material in admittedLive)
            {
                if (material == null ||
                    !EffectiveSelector(
                        material, out _, out var alphaRequest, out _))
                {
                    continue;
                }

                foreach (var textureProperty in alphaRequest.TextureProperties)
                {
                    parts.Add(AssetIdentity(
                        material.GetTexture(textureProperty.PropertyName)));
                }
            }

            foreach (var entry in
                     EffectiveMaterialMaterialization.CaptureBlockState(
                         renderer))
            {
                parts.Add(entry.SlotIndex.ToString(CultureInfo.InvariantCulture)
                    + ":" + entry.Name
                    + ":" + entry.Type
                    + ":" + entry.FloatValue.ToString(
                        CultureInfo.InvariantCulture)
                    + ":" + entry.VectorValue
                    + ":" + AssetIdentity(entry.TextureValue));
            }

            return string.Join(BodySeparator, parts);
        }

        /// <summary>
        /// One cache-stable identity string per asset: GUID plus source
        /// and meta file ticks when the asset is persistent, instance ID
        /// plus the update counter otherwise. The ticks read needs a real
        /// file, so an empty path never reaches it.
        /// </summary>
        private static string AssetIdentity(Object asset)
        {
            if (asset == null)
            {
                return "null";
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset, out var guid, out long localId))
            {
                return guid
                    + ":" + localId.ToString(CultureInfo.InvariantCulture)
                    + "\u001e" + FileTicks(path)
                    + "\u001e" + FileTicks(path + ".meta");
            }

            var identity = "instance:" + asset.GetInstanceID()
                .ToString(CultureInfo.InvariantCulture);
            if (asset is Texture texture)
            {
                identity += ":u" + texture.updateCount
                    .ToString(CultureInfo.InvariantCulture);
            }

            return identity;
        }

        private static string FileTicks(string path)
        {
            return File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                    .Ticks.ToString(CultureInfo.InvariantCulture)
                : "none";
        }
    }
}
