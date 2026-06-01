using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// 5-Stage Pipeline Architecture for HybridCPU (Iteration 6)
            ///
            /// Pipeline Stages:
            /// 1. Fetch (IF):    Fetch VLIW bundle from memory/cache
            /// 2. Decode (ID):   Decode instructions, read register operands
            /// 3. Execute (EX):  Execute ALU/vector operations
            /// 4. Memory (MEM):  Access memory for load/store operations
            /// 5. WriteBack (WB): Write results back to registers
            ///
            /// Goals:
            /// - Increase instruction throughput (IPC closer to 1.0)
            /// - Overlap memory access with computation
            /// - Support burst I/O pipelining for stream operations
            /// - Double buffering for ScratchA/B to hide memory latency
            ///
            /// Hazards:
            /// - Data hazards: RAW (Read After Write), WAR, WAW
            /// - Control hazards: Branches, jumps
            /// - Structural hazards: Resource conflicts (memory, ALU)
            ///
            /// Solutions:
            /// - Data forwarding (bypass) between stages
            /// - Pipeline stalls on dependencies
            /// - Branch prediction (simple: predict not taken)
            /// - Double buffering to avoid structural hazards
            /// </summary>

            // Pipeline stage registers (persistent state)
            private FetchStage pipeIF;
            private DecodeStage pipeID;
            private ExecuteStage pipeEX;
            private MemoryStage pipeMEM;
            private WriteBackStage pipeWB;
            private PipelineControl pipeCtrl;

            // Data forwarding paths
            private ForwardingPath forwardEX;   // EX stage forwarding (EX→EX bypass)
            private ForwardingPath forwardMEM;  // MEM stage forwarding (MEM→EX bypass)
            private ForwardingPath forwardWB;   // WB stage forwarding (WB→EX bypass)

            // Branch prediction
            private BranchPredictor branchPred;

            // FSP (Fine-Grained Slot Pilfering) components
            private Core.MicroOpScheduler _fspScheduler;

            /// <summary>
            /// MicroOp Loop Buffer for decode-once-replay-N during strip-mining (req.md §3).
            /// Caches decoded MicroOps so the core avoids re-fetching the 2048-bit bundle
            /// from L1-I on every strip-mining iteration.
            /// </summary>
            private Core.LoopBuffer _loopBuffer;

            /// <summary>
            /// Per-core replay fetch buffer reused across loop-buffer hits so replay fetch
            /// does not allocate a fresh 8-slot carrier array on every attempt.
            /// </summary>
            private Core.MicroOp?[]? _replayFetchBuffer;

            /// <summary>
            /// Per-core fetched-bundle byte buffer reused across live fetches so the frontend
            /// does not allocate a fresh 256-byte array each time `pipeIF` is populated.
            /// </summary>
            private byte[]? _fetchVliwBuffer;

            /// <summary>
            /// Per-core direct-read buffer reused by synchronous explicit-packet memory loads
            /// so the fallback main-memory path does not allocate a fresh tiny byte array per access.
            /// </summary>
            private byte[]? _explicitPacketImmediateReadBuffer;

            /// <summary>
            /// Current slot index within the fetched VLIW bundle (0-7).
            /// Variant A: slots are decoded one per cycle sequentially.
            /// After slot 7, the bundle is consumed and IP advances by 256.
            /// </summary>
            private byte pipelineBundleSlot;

            /// <summary>
            /// Unified runtime state for the current decoded bundle. This owns the canonical decode,
            /// legality descriptor, and published transport facts as one coherent bundle-scoped contract.
            /// </summary>
            private Core.DecodedBundleRuntimeState decodedBundleRuntimeState;
            private Core.BundleProgressState decodedBundleProgressState;
            /// <summary>
            /// Active derived execution layout for the current bundle (for example FSP packing).
            /// This remains runtime-only and must not overwrite the canonical/base decode transport.
            /// </summary>
            private Core.DecodedBundleDerivedIssuePlanState decodedBundleDerivedIssuePlanState;
            private ulong decodedBundleStateEpochCounter;
            private ulong decodedBundleStateVersionCounter;
            private Core.PipelineContourCertificate decodePublicationCertificate;
            private Core.PipelineContourCertificate executeCompletionCertificate;
            private Core.PipelineContourCertificate retireVisibilityCertificate;

            /// <summary>
            /// Read-only decoder-to-pipeline seam carrying the current slot's prepared
            /// cluster handoff metadata. This is advisory runtime input only and does not
            /// alter the reference narrow execution path.
            /// </summary>
            private Core.ClusterIssuePreparation pipeIDClusterPreparation;

            /// <summary>
            /// Runtime-readable admission-preparation snapshot derived from the cluster seam.
            /// This stays advisory-only until the future cluster admission path consumes it.
            /// </summary>
            private Core.RuntimeClusterAdmissionPreparation pipeIDAdmissionPreparation;

            /// <summary>
            /// Advisory internal admission draft derived from the bundle descriptor and runtime-readable
            /// preparation seam. This is diagnostic/runtime-readable preparation only.
            /// </summary>
            private Core.RuntimeClusterAdmissionCandidateView pipeIDAdmissionCandidateView;

            /// <summary>
            /// Advisory internal admission decision draft consumed from the candidate view.
            /// It prepares future admission-facing runtime logic while keeping legacy issue authoritative.
            /// </summary>
            private Core.RuntimeClusterAdmissionDecisionDraft pipeIDAdmissionDecisionDraft;

            /// <summary>
            /// Advisory runtime handoff snapshot exported from Phase 01 toward future legality and
            /// cluster-integration work. This does not change the reference execution path.
            /// </summary>
            private Core.RuntimeClusterAdmissionHandoff pipeIDAdmissionHandoff;

            /// <summary>
            /// Phase 05: Append-only differential trace capture for A/B verification.
            /// Collects advisory chain metadata per decoded bundle when differential tracing is active.
            /// Diagnostic-only — does not modify pipeline execution.
            /// </summary>
            private Core.DifferentialTraceCapture differentialTraceCapture;

            /// <summary>
            /// Flag indicating whether the current bundle has been decoded and packed with FSP.
            /// </summary>
            private bool bundleDecodedAndPacked;

            /// <summary>
            /// Initialize pipeline - called once during CPU_Core initialization
            /// </summary>
            public void InitializePipeline()
            {
                pipeIF.Clear();
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();
                ResetAssistRuntimeState();
                pipeCtrl.Clear();
                pipeCtrl.Enabled = false;
                pipelineBundleSlot = 0;

                // Initialize FSP bundle decode state
                ResetPipelineContourCertificates();
                decodedBundleStateEpochCounter = 0;
                decodedBundleStateVersionCounter = 0;
                ResetCurrentDecodedBundleSlotCarrierState(0);
                decodedBundleProgressState = Core.BundleProgressState.CreateEmpty();
                decodedBundleDerivedIssuePlanState = Core.DecodedBundleDerivedIssuePlanState.CreateEmpty();
                pipeIDClusterPreparation = Core.ClusterIssuePreparation.CreateEmpty();
                pipeIDAdmissionPreparation = Core.RuntimeClusterAdmissionPreparation.CreateEmpty();
                pipeIDAdmissionCandidateView = Core.RuntimeClusterAdmissionCandidateView.CreateEmpty();
                pipeIDAdmissionDecisionDraft = Core.RuntimeClusterAdmissionDecisionDraft.CreateEmpty();
                pipeIDAdmissionHandoff = Core.RuntimeClusterAdmissionHandoff.CreateEmpty();
                bundleDecodedAndPacked = false;

                // Clear forwarding paths
                forwardEX.Clear();
                forwardMEM.Clear();
                forwardWB.Clear();

                // Clear branch predictor
                branchPred.Clear();

                // Use Pod-local FSP scheduler if the topology is initialized (req.md §1).
                // Runtime legality authority now lives behind MicroOpScheduler ->
                // IRuntimeLegalityService, so the pipeline shell does not keep a
                // direct verifier instance anymore.
                var pod = Processor.GetPodForCore((int)this.CoreID);
                _fspScheduler = pod?.Scheduler ?? new Core.MicroOpScheduler();

                // Initialize Loop Buffer (req.md §3)
                _loopBuffer = new Core.LoopBuffer();
                _loopBuffer.Initialize();
                _replayFetchBuffer ??= new Core.MicroOp?[Core.BundleMetadata.BundleSlotCount];
                _fetchVliwBuffer ??= new byte[256];
                _explicitPacketImmediateReadBuffer ??= new byte[8];
            }

            /// <summary>
            /// Enable or disable pipeline mode
            /// </summary>
            public void SetPipelineMode(bool enable)
            {
                pipeCtrl.Enabled = enable;
                pipeCtrl.ClusterPreparedModeEnabled = enable; // Stage 7 Phase A: toggle wide-path with pipeline
                if (enable)
                {
                    SynchronizeExecutionMode();
                    FlushPipeline();
                }
            }

            /// <summary>
            /// Prepare a core for a fresh execution run using the unified phase-04
            /// start-state reset helper, then re-enable pipeline mode without
            /// rebuilding the already reset pipeline a second time.
            /// </summary>
            public void PrepareExecutionStart(ulong pc, int activeVtId = 0)
            {
                SynchronizeExecutionMode();
                ResetExecutionStartPcState(pc, activeVtId);
                pipeCtrl.Enabled = true;
                pipeCtrl.ClusterPreparedModeEnabled = true;
            }

            /// <summary>
            /// Stage 7 Phase A: explicit override for cluster-prepared wide-path mode.
            /// Allows test/CSR control independent of pipeline enable/disable.
            /// </summary>
            public void SetClusterPreparedMode(bool enable)
            {
                pipeCtrl.ClusterPreparedModeEnabled = enable;
            }

            /// <summary>
            /// Flush all pipeline stages (e.g., on branch mispredict)
            /// </summary>
            public void FlushPipeline(
                Core.AssistInvalidationReason assistInvalidationReason = Core.AssistInvalidationReason.PipelineFlush)
            {
                CancelInFlightExplicitMemoryRequests();

                // Refactoring Pt. 4 / Priority 4C: release GRLB and MSHR bookkeeping for
                // all occupied scalar lanes before clearing stage registers.
                ReleaseAllInFlightLaneAwareBookkeeping();
                InvalidateAssistRuntime(assistInvalidationReason);

                pipeIF.Clear();
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();
                pipeCtrl.Stalled = false;
                pipeCtrl.StallReason = PipelineStallKind.None;

                // Reset FSP bundle decode state
                pipelineBundleSlot = 0;
                bundleDecodedAndPacked = false;
                ResetCurrentDecodedBundleSlotCarrierState(0);
                decodedBundleProgressState = Core.BundleProgressState.CreateEmpty();
                decodedBundleDerivedIssuePlanState = Core.DecodedBundleDerivedIssuePlanState.CreateEmpty();
                pipeIDClusterPreparation = Core.ClusterIssuePreparation.CreateEmpty();
                pipeIDAdmissionPreparation = Core.RuntimeClusterAdmissionPreparation.CreateEmpty();
                pipeIDAdmissionCandidateView = Core.RuntimeClusterAdmissionCandidateView.CreateEmpty();
                pipeIDAdmissionDecisionDraft = Core.RuntimeClusterAdmissionDecisionDraft.CreateEmpty();
                pipeIDAdmissionHandoff = Core.RuntimeClusterAdmissionHandoff.CreateEmpty();
                // Invalidate Loop Buffer on pipeline flush (branch, interrupt)
                _loopBuffer.Invalidate(
                    ResolveReplayPhaseInvalidationReasonForPipelineFlush(
                        assistInvalidationReason));
                _fspScheduler?.SetReplayPhaseContext(
                    _loopBuffer.CurrentReplayPhase,
                    invalidateAssistOnDeactivate: false);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.ReplayPhaseInvalidationReason ResolveReplayPhaseInvalidationReasonForPipelineFlush(
                Core.AssistInvalidationReason assistInvalidationReason)
            {
                return assistInvalidationReason switch
                {
                    Core.AssistInvalidationReason.Trap or
                    Core.AssistInvalidationReason.Fence or
                    Core.AssistInvalidationReason.VmTransition or
                    Core.AssistInvalidationReason.SerializingBoundary
                        => Core.ReplayPhaseInvalidationReason.SerializingEvent,
                    _ => Core.ReplayPhaseInvalidationReason.Manual
                };
            }

            /// <summary>
            /// Get pipeline control for monitoring/debugging
            /// </summary>
            public PipelineControl GetPipelineControl()
            {
                return pipeCtrl;
            }

            /// <summary>
            /// Get Phase 1 replay metrics exported by the loop buffer.
            /// </summary>
            public Core.ReplayPhaseMetrics GetReplayPhaseMetrics()
            {
                return _loopBuffer.GetReplayPhaseMetrics();
            }

            /// <summary>
            /// Get the current replay phase context.
            /// </summary>
            public Core.ReplayPhaseContext GetReplayPhaseContext()
            {
                return _loopBuffer.CurrentReplayPhase;
            }

            /// <summary>
            /// Get scheduler Phase 1 telemetry.
            /// </summary>
            public Core.SchedulerPhaseMetrics GetSchedulerPhaseMetrics()
            {
                return _fspScheduler?.GetPhaseMetrics() ?? default;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessors for UI
            /// Get Fetch stage state
            /// </summary>
            public FetchStage GetFetchStage()
            {
                return pipeIF;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessors for UI
            /// Get Decode stage state
            /// </summary>
            public DecodeStage GetDecodeStage()
            {
                return pipeID;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessors for UI
            /// Get Execute stage state
            /// </summary>
            public ExecuteStage GetExecuteStage()
            {
                return pipeEX;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessor for UI and diagnostics.
            /// Returns a specific execute-stage scalar lane snapshot.
            /// </summary>
            public ScalarExecuteLaneState GetExecuteStageLane(byte laneIndex)
            {
                return laneIndex switch
                {
                    0 => pipeEX.Lane0,
                    1 => pipeEX.Lane1,
                    2 => pipeEX.Lane2,
                    3 => pipeEX.Lane3,
                    _ => CreateEmptyExecuteLaneState(laneIndex)
                };
            }

            /// <summary>
            /// PHASE 1: Public read-only accessor for UI and diagnostics.
            /// Returns the currently active execute-stage scalar lane index.
            /// </summary>
            public byte GetExecuteStageActiveLaneIndex()
            {
                return pipeEX.ActiveLaneIndex;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessors for UI
            /// Get Memory stage state
            /// </summary>
            public MemoryStage GetMemoryStage()
            {
                return pipeMEM;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessor for UI and diagnostics.
            /// Returns a specific memory-stage scalar lane snapshot.
            /// </summary>
            public ScalarMemoryLaneState GetMemoryStageLane(byte laneIndex)
            {
                return laneIndex switch
                {
                    0 => pipeMEM.Lane0,
                    1 => pipeMEM.Lane1,
                    2 => pipeMEM.Lane2,
                    3 => pipeMEM.Lane3,
                    _ => CreateEmptyMemoryLaneState(laneIndex)
                };
            }

            /// <summary>
            /// PHASE 1: Public read-only accessor for UI and diagnostics.
            /// Returns the currently active memory-stage scalar lane index.
            /// </summary>
            public byte GetMemoryStageActiveLaneIndex()
            {
                return pipeMEM.ActiveLaneIndex;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessors for UI
            /// Get WriteBack stage state
            /// </summary>
            public WriteBackStage GetWriteBackStage()
            {
                return pipeWB;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessor for UI and diagnostics.
            /// Returns a specific write-back-stage scalar lane snapshot.
            /// </summary>
            public ScalarWriteBackLaneState GetWriteBackStageLane(byte laneIndex)
            {
                return laneIndex switch
                {
                    0 => pipeWB.Lane0,
                    1 => pipeWB.Lane1,
                    2 => pipeWB.Lane2,
                    3 => pipeWB.Lane3,
                    _ => CreateEmptyWriteBackLaneState(laneIndex)
                };
            }

            /// <summary>
            /// PHASE 1: Public read-only accessor for UI and diagnostics.
            /// Returns the currently active write-back-stage scalar lane index.
            /// </summary>
            public byte GetWriteBackStageActiveLaneIndex()
            {
                return pipeWB.ActiveLaneIndex;
            }

            /// <summary>
            /// PHASE 1: Public read-only accessors for UI
            /// Get current bundle slot index (0-7)
            /// </summary>
            public byte GetPipelineBundleSlot()
            {
                return pipelineBundleSlot;
            }

            /// <summary>
            /// Accumulate double-buffer overlap cycle counters.
            /// Called by StreamEngine after a double-buffered execution completes.
            /// </summary>
            public void AccumulateOverlapCounters(
                ulong burstReadCycles,
                ulong burstWriteCycles,
                ulong computeCycles,
                ulong overlappedCycles)
            {
                pipeCtrl.BurstReadCycles += burstReadCycles;
                pipeCtrl.BurstWriteCycles += burstWriteCycles;
                pipeCtrl.ComputeCycles += computeCycles;
                pipeCtrl.OverlappedCycles += overlappedCycles;
            }

            private static ScalarExecuteLaneState CreateEmptyExecuteLaneState(byte laneIndex)
            {
                ScalarExecuteLaneState lane = new();
                lane.Clear(laneIndex);
                return lane;
            }

            private static ScalarMemoryLaneState CreateEmptyMemoryLaneState(byte laneIndex)
            {
                ScalarMemoryLaneState lane = new();
                lane.Clear(laneIndex);
                return lane;
            }

            private static ScalarWriteBackLaneState CreateEmptyWriteBackLaneState(byte laneIndex)
            {
                ScalarWriteBackLaneState lane = new();
                lane.Clear(laneIndex);
                return lane;
            }
        }
    }
}
