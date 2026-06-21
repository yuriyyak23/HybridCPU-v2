
using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Core
{
    // Phase 11 partial — v4 trace recording lives below, in the TraceSink class.
    /// <summary>
    /// Trace format for execution events
    /// </summary>
    public enum TraceFormat
    {
        CSV,
        JSON,
        Binary  // Phase 5: Binary format for efficient storage
    }

    /// <summary>
    /// Trace level for controlling trace detail
    /// </summary>
    public enum TraceLevel
    {
        None,       // No tracing
        Summary,    // PC, opcode, exception code only
        Full        // Complete trace with all operands and results (Phase 5: includes full architectural state)
    }

    /// <summary>
    /// Single execution event for tracing
    /// </summary>
    public struct TraceEvent
    {
        public long PC;
        public int BundleId;
        public int OpIndex;
        public uint Opcode;
        public object[] Operands;
        public ulong PredicateMask;
        public VectorExceptionFlags Flags;
        public object Result;
        public uint ExceptionCount;

        public TraceEvent(long pc, int bundleId, int opIndex, uint opcode)
        {
            PC = pc;
            BundleId = bundleId;
            OpIndex = opIndex;
            Opcode = opcode;
            Operands = Array.Empty<object>();
            PredicateMask = 0xFFFFFFFFFFFFFFFFUL;
            Flags = default;
            Result = null;
            ExceptionCount = 0;
        }
    }

    /// <summary>
    /// Vector exception flags for tracing
    /// </summary>
    public struct VectorExceptionFlags
    {
        public bool Overflow;
        public bool Underflow;
        public bool DivByZero;
        public bool InvalidOp;
        public bool Inexact;

        public bool HasException => Overflow || Underflow || DivByZero || InvalidOp || Inexact;

        public override string ToString()
        {
            if (!HasException) return "None";
            var flags = new List<string>();
            if (Overflow) flags.Add("OV");
            if (Underflow) flags.Add("UF");
            if (DivByZero) flags.Add("DZ");
            if (InvalidOp) flags.Add("IO");
            if (Inexact) flags.Add("IX");
            return string.Join("|", flags);
        }
    }

    /// <summary>
    /// Memory delta for incremental state tracking (Phase 5)
    /// </summary>
    public struct MemoryDelta
    {
        public ulong Address;
        public byte[] OldData;
        public byte[] NewData;

        public MemoryDelta(ulong address, byte[] oldData, byte[] newData)
        {
            Address = address;
            OldData = oldData;
            NewData = newData;
        }
    }

    /// <summary>
    /// FP exception context per thread (Phase 5)
    /// </summary>
    public struct FPExceptionContext
    {
        public VectorExceptionFlags Flags;
        public int RoundingMode;      // 0=RNE, 1=RTZ, 2=RDN, 3=RUP, 4=RMM
        public bool FlushToZero;
        public bool DefaultNaN;

        public FPExceptionContext()
        {
            Flags = default;
            RoundingMode = 0; // RNE by default
            FlushToZero = false;
            DefaultNaN = false;
        }
    }

    /// <summary>
    /// Optional typed-slot annotation for trace events (Phase 08).
    /// Emitted at Full trace level when FSP injection occurs.
    /// </summary>
    public struct TypedSlotTraceAnnotation
    {
        public SlotClass CandidateClass;
        public SlotPinningKind PinningKind;
        public int SelectedLane;
        public bool UsedFastPath;
        public bool UsedReplayHint;
        public TypedSlotRejectReason RejectReason;
    }

    /// <summary>
    /// Comprehensive trace event for deterministic replay (Phase 5)
    /// </summary>
    public struct FullStateTraceEvent
    {
        // Existing fields from TraceEvent
        public long PC;
        public int BundleId;
        public int OpIndex;
        public uint Opcode;

        // Extended fields for full state
        public int ThreadId;                     // Which thread executed this
        public long CycleNumber;                 // Global cycle counter
        public ulong[]? RegisterFile;            // All 32 int registers
        public ulong[]? VectorRegisters;         // VLMAX vector regs (if used)
        public ushort[]? PredicateRegisters;     // 16x 64-bit predicate regs
        public FPExceptionContext FPContext;     // FP exception state
        public MemoryDelta[]? MemoryWrites;      // Memory changes this cycle
        public bool WasStolenSlot;               // Was this FSP-injected?
        public int OriginalThreadId;             // If stolen, original owner

        // Pipeline state
        public string? PipelineStage;            // IF/ID/EX/MEM/WB
        public bool Stalled;                     // Was pipeline stalled?
        public string? StallReason;              // Why stalled
        public DecodedBundleStateOwnerKind DecodedBundleStateOwnerKind;
        public ulong DecodedBundleStateEpoch;
        public ulong DecodedBundleStateVersion;
        public DecodedBundleStateKind DecodedBundleStateKind;
        public DecodedBundleStateOrigin DecodedBundleStateOrigin;
        public ulong DecodedBundlePc;
        public byte DecodedBundleValidMask;
        public byte DecodedBundleNopMask;
        public bool DecodedBundleHasCanonicalDecode;
        public bool DecodedBundleHasCanonicalLegality;
        public bool DecodedBundleHasDecodeFault;

        // Memory subsystem state
        public int[]? BankQueueDepths;           // Per-bank queue depths
        public int ActiveMemoryRequests;         // In-flight requests
        public long MemorySubsystemCycle;        // Memory subsystem clock

        // Scheduler compatibility state. CurrentFSPPolicy is a legacy trace field
        // interpreted as the replay-aware bundle densification policy label.
        public int[]? ThreadReadyQueueDepths;    // Per-thread ready queue
        public string? CurrentFSPPolicy;         // Legacy trace field retained for binary compatibility

        // Phase-aware replay state
        public ulong ReplayEpochId;
        public ulong ReplayPhaseCachedPc;
        public ulong ReplayEpochLength;
        public int ReplayPhaseValidSlotCount;
        public byte StableDonorMask;
        public ReplayPhaseInvalidationReason ReplayInvalidationReason;
        public bool PhaseCertificateTemplateReusable;
        public long PhaseCertificateReadyHits;
        public long PhaseCertificateReadyMisses;
        public long EstimatedPhaseCertificateChecksSaved;
        public long PhaseCertificateInvalidations;
        public ReplayPhaseInvalidationReason PhaseCertificateInvalidationReason;
        public long DeterminismReferenceOpportunitySlots;
        public long DeterminismReplayEligibleSlots;
        public long DeterminismMaskedSlots;
        public long DeterminismEstimatedLostSlots;
        public long DeterminismConstrainedCycles;
        public long DomainIsolationProbeAttempts;
        public long DomainIsolationBlockedAttempts;
        public long DomainIsolationCrossDomainBlocks;
        public long DomainIsolationKernelToUserBlocks;

        // Phase 06: explicit scheduler/FSM eligibility diagnostics
        public long EligibilityMaskedCycles;
        public long EligibilityMaskedReadyCandidates;
        public byte LastEligibilityRequestedMask;
        public byte LastEligibilityNormalizedMask;
        public byte LastEligibilityReadyPortMask;
        public byte LastEligibilityVisibleReadyMask;
        public byte LastEligibilityMaskedReadyMask;

        // Post-phase-08 assist runtime telemetry
        public long AssistNominations;
        public long AssistInjections;
        public long AssistRejects;
        public long AssistBoundaryRejects;
        public long AssistInvalidations;
        public long AssistInterCoreNominations;
        public long AssistInterCoreInjections;
        public long AssistInterCoreRejects;
        public long AssistInterCoreDomainRejects;
        public long AssistInterCorePodLocalInjections;
        public long AssistInterCoreCrossPodInjections;
        public long AssistInterCorePodLocalRejects;
        public long AssistInterCoreCrossPodRejects;
        public long AssistInterCorePodLocalDomainRejects;
        public long AssistInterCoreCrossPodDomainRejects;
        public long AssistInterCoreSameVtVectorInjects;
        public long AssistInterCoreDonorVtVectorInjects;
        public long AssistInterCoreSameVtVectorWritebackInjects;
        public long AssistInterCoreDonorVtVectorWritebackInjects;
        public long AssistInterCoreLane6DefaultStoreDonorPrefetchInjects;
        public long AssistInterCoreLane6HotLoadDonorPrefetchInjects;
        public long AssistInterCoreLane6HotStoreDonorPrefetchInjects;
        public long AssistInterCoreLane6DonorPrefetchInjects;
        public long AssistInterCoreLane6ColdStoreLdsaInjects;
        public long AssistInterCoreLane6LdsaInjects;
        public long AssistQuotaRejects;
        public long AssistQuotaIssueRejects;
        public long AssistQuotaLineRejects;
        public long AssistQuotaLinesReserved;
        public long AssistBackpressureRejects;
        public long AssistBackpressureOuterCapRejects;
        public long AssistBackpressureMshrRejects;
        public long AssistBackpressureDmaSrfRejects;
        public long AssistDonorPrefetchInjects;
        public long AssistLdsaInjects;
        public long AssistVdsaInjects;
        public long AssistSameVtInjects;
        public long AssistDonorVtInjects;
        public AssistInvalidationReason AssistInvalidationReason;
        public ulong AssistOwnershipSignature;

        // Phase 08: typed-slot annotation (memory trace only until a later binary-trace revision)
        public TypedSlotTraceAnnotation? TypedSlotAnnotation;

        // Phase 05: pipeline FSM state at time of event
        public PipelineState CurrentPipelineState;
    }

    /// <summary>
    /// State checkpoint for incremental replay (Phase 5)
    /// </summary>
    public struct StateCheckpoint
    {
        public long CycleNumber;
        public byte[] ProcessorState;      // Serialized CPU state
        public byte[] MemorySnapshot;      // Compressed memory dump
        public ulong RandomState;          // RNG state for determinism
    }

    /// <summary>
    /// Pipeline FSM transition event (Phase 05).
    /// Recorded whenever the pipeline FSM changes state.
    /// </summary>
    public struct FsmTransitionEvent
    {
        /// <summary>Global cycle counter at the time of the transition.</summary>
        public long CycleNumber;

        /// <summary>Virtual thread (VT) ID that underwent the transition.</summary>
        public int VtId;

        /// <summary>Pipeline state before the transition.</summary>
        public PipelineState FromState;

        /// <summary>Trigger that caused the transition.</summary>
        public PipelineTransitionTrigger Trigger;

        /// <summary>Pipeline state after the transition.</summary>
        public PipelineState ToState;
    }

    /// <summary>
    /// TraceSink records execution events for verification and debugging.
    /// Phase 5: Enhanced with full state capture and checkpointing.
    /// Phase 11: Extended with v4 typed trace event recording (<see cref="IV4TraceEventSink"/>).
    /// </summary>
    public class TraceSink : IV4TraceEventSink
    {
        private const uint TraceMagic = 0x54524143; // 'TRAC' in ASCII for binary trace header
        internal const ushort BinaryTraceVersion = 23;

        private readonly List<TraceEvent> events;
        private readonly TraceFormat format;
        private readonly string filePath;
        private bool enabled;
        private TraceLevel level;

        // Phase 5: Per-thread full state traces
        private readonly Dictionary<int, List<FullStateTraceEvent>> _perThreadTraces;
        private long _checkpointInterval = 10000;  // Checkpoint every 10K cycles
        private readonly List<StateCheckpoint> _checkpoints;
        private long _currentCycle = 0;

        // Phase 05: FSM transition log
        private readonly List<FsmTransitionEvent> _fsmTransitions;

        // Phase 11: v4 typed trace event log (append-only)
        private readonly List<V4TraceEvent> _v4Events;

        public TraceSink(TraceFormat format = TraceFormat.CSV, string filePath = "trace.log")
        {
            this.events = new List<TraceEvent>();
            this.format = format;
            this.filePath = filePath;
            this.enabled = false;
            this.level = TraceLevel.Summary;

            // Phase 5: Initialize per-thread trace storage
            _perThreadTraces = new Dictionary<int, List<FullStateTraceEvent>>();
            _checkpoints = new List<StateCheckpoint>();
            _fsmTransitions = new List<FsmTransitionEvent>();

            // Phase 11: v4 event log
            _v4Events = new List<V4TraceEvent>();

            // Initialize 16 thread traces
            for (int i = 0; i < 16; i++)
            {
                _perThreadTraces[i] = new List<FullStateTraceEvent>();
            }
        }

        /// <summary>
        /// Enable or disable tracing
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;
        }

        /// <summary>
        /// Set trace level
        /// </summary>
        public void SetLevel(TraceLevel level)
        {
            this.level = level;
        }

        /// <summary>
        /// Returns true only when full-state trace payloads should be materialized and recorded.
        /// Callers can use this to avoid building register/predicate snapshots on summary or disabled paths.
        /// </summary>
        public bool ShouldCaptureFullState => enabled && level == TraceLevel.Full;

        /// <summary>
        /// Record a trace event
        /// </summary>
        public void Record(TraceEvent evt)
        {
            if (!enabled || level == TraceLevel.None)
                return;

            // For Summary level, clear operands and result to save memory
            if (level == TraceLevel.Summary)
            {
                evt.Operands = Array.Empty<object>();
                evt.Result = null;
            }

            events.Add(evt);
        }

        /// <summary>
        /// Clear all recorded events
        /// </summary>
        public void Clear()
        {
            events.Clear();
        }

        /// <summary>
        /// Get count of recorded events
        /// </summary>
        public int EventCount => events.Count;

        /// <summary>
        /// Set checkpoint interval (Phase 5)
        /// </summary>
        public void SetCheckpointInterval(long interval)
        {
            _checkpointInterval = interval;
        }

        /// <summary>
        /// Record full architectural state (Phase 5)
        /// </summary>
        public void RecordFullState(FullStateTraceEvent evt)
        {
            if (!ShouldCaptureFullState)
                return;

            if (evt.ThreadId >= 0 && evt.ThreadId < 16)
            {
                _perThreadTraces[evt.ThreadId].Add(evt);
                _currentCycle = evt.CycleNumber;

                // Check if we need to create a checkpoint
                if (_checkpointInterval > 0 && evt.CycleNumber % _checkpointInterval == 0)
                {
                    // Checkpoint creation would be triggered here
                    // For now, we mark the opportunity
                }
            }
        }

        /// <summary>
        /// Record a pipeline FSM state transition (Phase 05).
        /// Always recorded regardless of trace level — FSM transitions are
        /// architecturally significant and required for deterministic replay.
        /// </summary>
        public void RecordFsmTransition(
            long cycleNumber,
            int vtId,
            PipelineState fromState,
            PipelineTransitionTrigger trigger,
            PipelineState toState)
        {
            if (!enabled)
                return;

            _fsmTransitions.Add(new FsmTransitionEvent
            {
                CycleNumber = cycleNumber,
                VtId        = vtId,
                FromState   = fromState,
                Trigger     = trigger,
                ToState     = toState,
            });
        }

        /// <summary>
        /// Returns all recorded FSM transition events for inspection and replay.
        /// </summary>
        public IReadOnlyList<FsmTransitionEvent> GetFsmTransitions()
            => _fsmTransitions.AsReadOnly();

        /// <summary>
        /// Record a full-state trace event enriched with replay-phase diagnostics.
        /// </summary>
        public void RecordPhaseAwareState(
            FullStateTraceEvent evt,
            ReplayPhaseContext phaseContext,
            SchedulerPhaseMetrics schedulerMetrics,
            bool phaseCertificateTemplateReusable)
        {
            evt.ReplayEpochId = phaseContext.EpochId;
            evt.ReplayPhaseCachedPc = phaseContext.CachedPc;
            evt.ReplayEpochLength = phaseContext.EpochLength;
            evt.ReplayPhaseValidSlotCount = phaseContext.ValidSlotCount;
            evt.StableDonorMask = phaseContext.StableDonorMask;
            evt.ReplayInvalidationReason = phaseContext.LastInvalidationReason;
            evt.PhaseCertificateTemplateReusable = phaseCertificateTemplateReusable;
            evt.PhaseCertificateReadyHits = schedulerMetrics.PhaseCertificateReadyHits;
            evt.PhaseCertificateReadyMisses = schedulerMetrics.PhaseCertificateReadyMisses;
            evt.EstimatedPhaseCertificateChecksSaved = schedulerMetrics.EstimatedChecksSaved;
            evt.PhaseCertificateInvalidations = schedulerMetrics.PhaseCertificateInvalidations;
            evt.PhaseCertificateInvalidationReason = schedulerMetrics.LastCertificateInvalidationReason;
            evt.DeterminismReferenceOpportunitySlots = schedulerMetrics.DeterminismReferenceOpportunitySlots;
            evt.DeterminismReplayEligibleSlots = schedulerMetrics.DeterminismReplayEligibleSlots;
            evt.DeterminismMaskedSlots = schedulerMetrics.DeterminismMaskedSlots;
            evt.DeterminismEstimatedLostSlots = schedulerMetrics.DeterminismEstimatedLostSlots;
            evt.DeterminismConstrainedCycles = schedulerMetrics.DeterminismConstrainedCycles;
            evt.DomainIsolationProbeAttempts = schedulerMetrics.DomainIsolationProbeAttempts;
            evt.DomainIsolationBlockedAttempts = schedulerMetrics.DomainIsolationBlockedAttempts;
            evt.DomainIsolationCrossDomainBlocks = schedulerMetrics.DomainIsolationCrossDomainBlocks;
            evt.DomainIsolationKernelToUserBlocks = schedulerMetrics.DomainIsolationKernelToUserBlocks;
            evt.EligibilityMaskedCycles = schedulerMetrics.EligibilityMaskedCycles;
            evt.EligibilityMaskedReadyCandidates = schedulerMetrics.EligibilityMaskedReadyCandidates;
            evt.LastEligibilityRequestedMask = schedulerMetrics.LastEligibilityRequestedMask;
            evt.LastEligibilityNormalizedMask = schedulerMetrics.LastEligibilityNormalizedMask;
            evt.LastEligibilityReadyPortMask = schedulerMetrics.LastEligibilityReadyPortMask;
            evt.LastEligibilityVisibleReadyMask = schedulerMetrics.LastEligibilityVisibleReadyMask;
            evt.LastEligibilityMaskedReadyMask = schedulerMetrics.LastEligibilityMaskedReadyMask;
            evt.AssistNominations = schedulerMetrics.AssistNominations;
            evt.AssistInjections = schedulerMetrics.AssistInjections;
            evt.AssistRejects = schedulerMetrics.AssistRejects;
            evt.AssistBoundaryRejects = schedulerMetrics.AssistBoundaryRejects;
            evt.AssistInvalidations = schedulerMetrics.AssistInvalidations;
            evt.AssistInterCoreNominations = schedulerMetrics.AssistInterCoreNominations;
            evt.AssistInterCoreInjections = schedulerMetrics.AssistInterCoreInjections;
            evt.AssistInterCoreRejects = schedulerMetrics.AssistInterCoreRejects;
            evt.AssistInterCoreDomainRejects = schedulerMetrics.AssistInterCoreDomainRejects;
            evt.AssistInterCorePodLocalInjections = schedulerMetrics.AssistInterCorePodLocalInjections;
            evt.AssistInterCoreCrossPodInjections = schedulerMetrics.AssistInterCoreCrossPodInjections;
            evt.AssistInterCorePodLocalRejects = schedulerMetrics.AssistInterCorePodLocalRejects;
            evt.AssistInterCoreCrossPodRejects = schedulerMetrics.AssistInterCoreCrossPodRejects;
            evt.AssistInterCorePodLocalDomainRejects = schedulerMetrics.AssistInterCorePodLocalDomainRejects;
            evt.AssistInterCoreCrossPodDomainRejects = schedulerMetrics.AssistInterCoreCrossPodDomainRejects;
            evt.AssistInterCoreSameVtVectorInjects = schedulerMetrics.AssistInterCoreSameVtVectorInjects;
            evt.AssistInterCoreDonorVtVectorInjects = schedulerMetrics.AssistInterCoreDonorVtVectorInjects;
            evt.AssistInterCoreSameVtVectorWritebackInjects = schedulerMetrics.AssistInterCoreSameVtVectorWritebackInjects;
            evt.AssistInterCoreDonorVtVectorWritebackInjects = schedulerMetrics.AssistInterCoreDonorVtVectorWritebackInjects;
            evt.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects = schedulerMetrics.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects;
            evt.AssistInterCoreLane6HotLoadDonorPrefetchInjects = schedulerMetrics.AssistInterCoreLane6HotLoadDonorPrefetchInjects;
            evt.AssistInterCoreLane6HotStoreDonorPrefetchInjects = schedulerMetrics.AssistInterCoreLane6HotStoreDonorPrefetchInjects;
            evt.AssistInterCoreLane6DonorPrefetchInjects = schedulerMetrics.AssistInterCoreLane6DonorPrefetchInjects;
            evt.AssistInterCoreLane6ColdStoreLdsaInjects = schedulerMetrics.AssistInterCoreLane6ColdStoreLdsaInjects;
            evt.AssistInterCoreLane6LdsaInjects = schedulerMetrics.AssistInterCoreLane6LdsaInjects;
            evt.AssistQuotaRejects = schedulerMetrics.AssistQuotaRejects;
            evt.AssistQuotaIssueRejects = schedulerMetrics.AssistQuotaIssueRejects;
            evt.AssistQuotaLineRejects = schedulerMetrics.AssistQuotaLineRejects;
            evt.AssistQuotaLinesReserved = schedulerMetrics.AssistQuotaLinesReserved;
            evt.AssistBackpressureRejects = schedulerMetrics.AssistBackpressureRejects;
            evt.AssistBackpressureOuterCapRejects = schedulerMetrics.AssistBackpressureOuterCapRejects;
            evt.AssistBackpressureMshrRejects = schedulerMetrics.AssistBackpressureMshrRejects;
            evt.AssistBackpressureDmaSrfRejects = schedulerMetrics.AssistBackpressureDmaSrfRejects;
            evt.AssistDonorPrefetchInjects = schedulerMetrics.AssistDonorPrefetchInjects;
            evt.AssistLdsaInjects = schedulerMetrics.AssistLdsaInjects;
            evt.AssistVdsaInjects = schedulerMetrics.AssistVdsaInjects;
            evt.AssistSameVtInjects = schedulerMetrics.AssistSameVtInjects;
            evt.AssistDonorVtInjects = schedulerMetrics.AssistDonorVtInjects;
            evt.AssistInvalidationReason = schedulerMetrics.LastAssistInvalidationReason;
            evt.AssistOwnershipSignature = schedulerMetrics.LastAssistOwnershipSignature;
            RecordFullState(evt);
        }

        /// <summary>
        /// Get per-thread trace events (Phase 5)
        /// </summary>
        public IReadOnlyList<FullStateTraceEvent> GetThreadTrace(int threadId)
        {
            if (threadId >= 0 && threadId < 16 && _perThreadTraces.ContainsKey(threadId))
            {
                return _perThreadTraces[threadId].AsReadOnly();
            }
            return Array.Empty<FullStateTraceEvent>();
        }

        /// <summary>
        /// Get total trace count across all threads (Phase 5)
        /// </summary>
        public int GetTotalTraceCount()
        {
            int total = 0;
            foreach (var trace in _perThreadTraces.Values)
            {
                total += trace.Count;
            }
            return total;
        }

        /// <summary>
        /// Export binary trace in efficient format (Phase 5)
        /// </summary>
        public void ExportBinaryTrace(string binaryFilePath)
        {
            using var fs = new FileStream(binaryFilePath, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // Write header
            writer.Write(TraceMagic);
            writer.Write(BinaryTraceVersion);
            writer.Write(_perThreadTraces.Count);

            // Write checkpoints
            writer.Write(_checkpoints.Count);
            foreach (var checkpoint in _checkpoints)
            {
                SerializeCheckpoint(writer, checkpoint);
            }

            // Write per-thread traces
            foreach (var kvp in _perThreadTraces)
            {
                writer.Write(kvp.Key);  // Thread ID
                writer.Write(kvp.Value.Count);
                foreach (var evt in kvp.Value)
                {
                    SerializeFullStateEvent(writer, evt);
                }
            }
        }

        /// <summary>
        /// Serialize checkpoint to binary format (Phase 5)
        /// </summary>
        private void SerializeCheckpoint(BinaryWriter writer, StateCheckpoint checkpoint)
        {
            writer.Write(checkpoint.CycleNumber);
            writer.Write(checkpoint.ProcessorState?.Length ?? 0);
            if (checkpoint.ProcessorState != null)
            {
                writer.Write(checkpoint.ProcessorState);
            }
            writer.Write(checkpoint.MemorySnapshot?.Length ?? 0);
            if (checkpoint.MemorySnapshot != null)
            {
                writer.Write(checkpoint.MemorySnapshot);
            }
            writer.Write(checkpoint.RandomState);
        }

        /// <summary>
        /// Serialize full state trace event to binary format (Phase 5)
        /// </summary>
        private void SerializeFullStateEvent(BinaryWriter writer, FullStateTraceEvent evt)
        {
            writer.Write(evt.PC);
            writer.Write(evt.BundleId);
            writer.Write(evt.OpIndex);
            writer.Write(evt.Opcode);
            writer.Write(evt.ThreadId);
            writer.Write(evt.CycleNumber);

            // Register file
            writer.Write(evt.RegisterFile?.Length ?? 0);
            if (evt.RegisterFile != null)
            {
                foreach (var reg in evt.RegisterFile)
                {
                    writer.Write(reg);
                }
            }

            // Vector registers
            writer.Write(evt.VectorRegisters?.Length ?? 0);
            if (evt.VectorRegisters != null)
            {
                foreach (var vreg in evt.VectorRegisters)
                {
                    writer.Write(vreg);
                }
            }

            // Predicate registers
            writer.Write(evt.PredicateRegisters?.Length ?? 0);
            if (evt.PredicateRegisters != null)
            {
                foreach (var preg in evt.PredicateRegisters)
                {
                    writer.Write(preg);
                }
            }

            // FP context
            writer.Write(evt.FPContext.RoundingMode);
            writer.Write(evt.FPContext.FlushToZero);
            writer.Write(evt.FPContext.DefaultNaN);

            // Memory writes
            writer.Write(evt.MemoryWrites?.Length ?? 0);
            if (evt.MemoryWrites != null)
            {
                foreach (var delta in evt.MemoryWrites)
                {
                    writer.Write(delta.Address);
                    writer.Write(delta.OldData?.Length ?? 0);
                    if (delta.OldData != null)
                    {
                        writer.Write(delta.OldData);
                    }
                    writer.Write(delta.NewData?.Length ?? 0);
                    if (delta.NewData != null)
                    {
                        writer.Write(delta.NewData);
                    }
                }
            }

            // FSP state
            writer.Write(evt.WasStolenSlot);
            writer.Write(evt.OriginalThreadId);

            // Pipeline state
            writer.Write(evt.PipelineStage ?? "");
            writer.Write(evt.Stalled);
            writer.Write(evt.StallReason ?? "");
            writer.Write((byte)evt.DecodedBundleStateOwnerKind);
            writer.Write(evt.DecodedBundleStateEpoch);
            writer.Write(evt.DecodedBundleStateVersion);
            writer.Write((byte)evt.DecodedBundleStateKind);
            writer.Write((byte)evt.DecodedBundleStateOrigin);
            writer.Write(evt.DecodedBundlePc);
            writer.Write(evt.DecodedBundleValidMask);
            writer.Write(evt.DecodedBundleNopMask);
            writer.Write(evt.DecodedBundleHasCanonicalDecode);
            writer.Write(evt.DecodedBundleHasCanonicalLegality);
            writer.Write(evt.DecodedBundleHasDecodeFault);

            // Memory subsystem
            writer.Write(evt.BankQueueDepths?.Length ?? 0);
            if (evt.BankQueueDepths != null)
            {
                foreach (var depth in evt.BankQueueDepths)
                {
                    writer.Write(depth);
                }
            }
            writer.Write(evt.ActiveMemoryRequests);
            writer.Write(evt.MemorySubsystemCycle);

            // Scheduler state
            writer.Write(evt.ThreadReadyQueueDepths?.Length ?? 0);
            if (evt.ThreadReadyQueueDepths != null)
            {
                foreach (var depth in evt.ThreadReadyQueueDepths)
                {
                    writer.Write(depth);
                }
            }
            writer.Write(evt.CurrentFSPPolicy ?? "");

            // Phase-aware replay state
            writer.Write(evt.ReplayEpochId);
            writer.Write(evt.ReplayPhaseCachedPc);
            writer.Write(evt.ReplayEpochLength);
            writer.Write(evt.ReplayPhaseValidSlotCount);
            writer.Write(evt.StableDonorMask);
            writer.Write((byte)evt.ReplayInvalidationReason);
            writer.Write(evt.PhaseCertificateTemplateReusable);
            writer.Write(evt.PhaseCertificateReadyHits);
            writer.Write(evt.PhaseCertificateReadyMisses);
            writer.Write(evt.EstimatedPhaseCertificateChecksSaved);
            writer.Write(evt.PhaseCertificateInvalidations);
            writer.Write((byte)evt.PhaseCertificateInvalidationReason);
            writer.Write(evt.DeterminismReferenceOpportunitySlots);
            writer.Write(evt.DeterminismReplayEligibleSlots);
            writer.Write(evt.DeterminismMaskedSlots);
            writer.Write(evt.DeterminismEstimatedLostSlots);
            writer.Write(evt.DeterminismConstrainedCycles);
            writer.Write(evt.DomainIsolationProbeAttempts);
            writer.Write(evt.DomainIsolationBlockedAttempts);
            writer.Write(evt.DomainIsolationCrossDomainBlocks);
            writer.Write(evt.DomainIsolationKernelToUserBlocks);
            writer.Write(evt.EligibilityMaskedCycles);
            writer.Write(evt.EligibilityMaskedReadyCandidates);
            writer.Write(evt.LastEligibilityRequestedMask);
            writer.Write(evt.LastEligibilityNormalizedMask);
            writer.Write(evt.LastEligibilityReadyPortMask);
            writer.Write(evt.LastEligibilityVisibleReadyMask);
            writer.Write(evt.LastEligibilityMaskedReadyMask);
            writer.Write(evt.AssistNominations);
            writer.Write(evt.AssistInjections);
            writer.Write(evt.AssistRejects);
            writer.Write(evt.AssistBoundaryRejects);
            writer.Write(evt.AssistInvalidations);
            writer.Write(evt.AssistInterCoreNominations);
            writer.Write(evt.AssistInterCoreInjections);
            writer.Write(evt.AssistInterCoreRejects);
            writer.Write(evt.AssistInterCoreDomainRejects);
            writer.Write(evt.AssistInterCorePodLocalInjections);
            writer.Write(evt.AssistInterCoreCrossPodInjections);
            writer.Write(evt.AssistInterCorePodLocalRejects);
            writer.Write(evt.AssistInterCoreCrossPodRejects);
            writer.Write(evt.AssistInterCorePodLocalDomainRejects);
            writer.Write(evt.AssistInterCoreCrossPodDomainRejects);
            writer.Write(evt.AssistInterCoreSameVtVectorInjects);
            writer.Write(evt.AssistInterCoreDonorVtVectorInjects);
            writer.Write(evt.AssistInterCoreSameVtVectorWritebackInjects);
            writer.Write(evt.AssistInterCoreDonorVtVectorWritebackInjects);
            writer.Write(evt.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects);
            writer.Write(evt.AssistInterCoreLane6HotLoadDonorPrefetchInjects);
            writer.Write(evt.AssistInterCoreLane6HotStoreDonorPrefetchInjects);
            writer.Write(evt.AssistInterCoreLane6DonorPrefetchInjects);
            writer.Write(evt.AssistInterCoreLane6ColdStoreLdsaInjects);
            writer.Write(evt.AssistInterCoreLane6LdsaInjects);
            writer.Write(evt.AssistQuotaRejects);
            writer.Write(evt.AssistQuotaIssueRejects);
            writer.Write(evt.AssistQuotaLineRejects);
            writer.Write(evt.AssistQuotaLinesReserved);
            writer.Write(evt.AssistBackpressureRejects);
            writer.Write(evt.AssistBackpressureOuterCapRejects);
            writer.Write(evt.AssistBackpressureMshrRejects);
            writer.Write(evt.AssistBackpressureDmaSrfRejects);
            writer.Write(evt.AssistDonorPrefetchInjects);
            writer.Write(evt.AssistLdsaInjects);
            writer.Write(evt.AssistVdsaInjects);
            writer.Write(evt.AssistSameVtInjects);
            writer.Write(evt.AssistDonorVtInjects);
            writer.Write((byte)evt.AssistInvalidationReason);
            writer.Write(evt.AssistOwnershipSignature);
        }

        /// <summary>
        /// Clear all traces including per-thread traces (Phase 5)
        /// </summary>
        public void ClearAllTraces()
        {
            events.Clear();
            foreach (var trace in _perThreadTraces.Values)
            {
                trace.Clear();
            }
            _checkpoints.Clear();
            _currentCycle = 0;
        }

        /// <summary>
        /// Flush trace to file
        /// </summary>
        public void Flush()
        {
            if (events.Count == 0)
                return;

            switch (format)
            {
                case TraceFormat.CSV:
                    FlushCSV();
                    break;
                case TraceFormat.JSON:
                    FlushJSON();
                    break;
            }

            events.Clear();
        }

        private void FlushCSV()
        {
            using var writer = new StreamWriter(filePath, append: true);

            // Write header if file is empty
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                if (level == TraceLevel.Summary)
                {
                    writer.WriteLine("PC,BundleId,OpIndex,Opcode,ExceptionFlags,ExceptionCount");
                }
                else
                {
                    writer.WriteLine("PC,BundleId,OpIndex,Opcode,Operands,PredicateMask,ExceptionFlags,Result,ExceptionCount");
                }
            }

            foreach (var evt in events)
            {
                if (level == TraceLevel.Summary)
                {
                    writer.WriteLine($"{evt.PC},{evt.BundleId},{evt.OpIndex},{evt.Opcode},{evt.Flags},{evt.ExceptionCount}");
                }
                else
                {
                    var operands = evt.Operands != null && evt.Operands.Length > 0
                        ? string.Join(";", evt.Operands)
                        : "";
                    var result = evt.Result?.ToString() ?? "";
                    writer.WriteLine($"{evt.PC},{evt.BundleId},{evt.OpIndex},{evt.Opcode},\"{operands}\",0x{evt.PredicateMask:X16},{evt.Flags},\"{result}\",{evt.ExceptionCount}");
                }
            }
        }

        private void FlushJSON()
        {
            using var writer = new StreamWriter(filePath, append: true);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            foreach (var evt in events)
            {
                var jsonObj = new Dictionary<string, object>
                {
                    ["PC"] = evt.PC,
                    ["BundleId"] = evt.BundleId,
                    ["OpIndex"] = evt.OpIndex,
                    ["Opcode"] = evt.Opcode.ToString(),
                    ["ExceptionFlags"] = evt.Flags.ToString(),
                    ["ExceptionCount"] = evt.ExceptionCount
                };

                if (level == TraceLevel.Full)
                {
                    jsonObj["Operands"] = evt.Operands ?? Array.Empty<object>();
                    jsonObj["PredicateMask"] = $"0x{evt.PredicateMask:X16}";
                    jsonObj["Result"] = evt.Result?.ToString() ?? "";
                }

                var json = JsonSerializer.Serialize(jsonObj, options);
                writer.WriteLine(json);
            }
        }

        /// <summary>
        /// Get all recorded events (for testing)
        /// </summary>
        public IReadOnlyList<TraceEvent> GetEvents()
        {
            return events.AsReadOnly();
        }

        // ─── Phase 11: v4 Typed Trace Event Recording ───────────────────────

        /// <summary>
        /// Record a v4 typed trace event.
        /// <para>
        /// The trace is append-only — events cannot be retroactively modified.
        /// Recording is always performed regardless of <see cref="TraceLevel"/>;
        /// v4 events are architecturally significant.
        /// </para>
        /// </summary>
        /// <param name="evt">The v4 event to record.</param>
        public void RecordV4Event(V4TraceEvent evt)
        {
            if (!enabled)
                return;

            _v4Events.Add(evt);
        }

        /// <summary>
        /// Returns all recorded v4 trace events for inspection and replay validation.
        /// The list is append-only during execution.
        /// </summary>
        public IReadOnlyList<V4TraceEvent> GetV4Events()
            => _v4Events.AsReadOnly();

        /// <summary>
        /// Returns only the v4 events matching the given <paramref name="kind"/>.
        /// </summary>
        public IReadOnlyList<V4TraceEvent> GetV4Events(TraceEventKind kind)
        {
            var result = new List<V4TraceEvent>();
            foreach (var evt in _v4Events)
            {
                if (evt.Kind == kind)
                    result.Add(evt);
            }
            return result;
        }

        /// <summary>
        /// Returns only the v4 events emitted by the given <paramref name="vtId"/>.
        /// </summary>
        public IReadOnlyList<V4TraceEvent> GetV4EventsForVt(byte vtId)
        {
            var result = new List<V4TraceEvent>();
            foreach (var evt in _v4Events)
            {
                if (evt.VtId == vtId)
                    result.Add(evt);
            }
            return result;
        }

        /// <summary>
        /// Clear all v4 trace events (e.g., between test runs).
        /// </summary>
        public void ClearV4Events() => _v4Events.Clear();

        /// <summary>Total number of v4 events recorded.</summary>
        public int V4EventCount => _v4Events.Count;
    }
}
