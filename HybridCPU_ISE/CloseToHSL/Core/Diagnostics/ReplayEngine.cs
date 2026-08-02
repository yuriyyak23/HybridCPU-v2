using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Core
{
    /// <summary>
    /// Replay engine for deterministic execution reproduction (Phase 5)
    /// Supports checkpoint-based fast-forward and cycle-accurate replay
    /// </summary>
    public partial class ReplayEngine
    {
        private const string DenseTimelinePolicyName = "ReplayAwarePhase1.DenseTimeline";

        private static bool IsSupportedBinaryTraceVersion(ushort version)
        {
            // Live replay intentionally keeps only the current schema plus two
            // prior revisions. Older artifacts must be normalized offline before
            // they enter the deterministic replay path.
            const ushort liveReplayVersionWindow = 3;
            ushort currentVersion = TraceSink.BinaryTraceVersion;
            ushort oldestSupportedVersion = currentVersion >= liveReplayVersionWindow
                ? (ushort)(currentVersion - liveReplayVersionWindow + 1)
                : (ushort)1;

            return version >= oldestSupportedVersion && version <= currentVersion;
        }

        private TraceSink? _trace;
        private int _currentCheckpointIndex = 0;
        private readonly Dictionary<int, int> _perThreadEventIndex;
        private readonly List<StateCheckpoint> _checkpoints;
        private readonly Dictionary<int, List<FullStateTraceEvent>> _perThreadTraces;
        private string _traceFilePath;

        public ReplayEngine(string traceFilePath = "")
        {
            _traceFilePath = traceFilePath;
            _perThreadEventIndex = new Dictionary<int, int>();
            _checkpoints = new List<StateCheckpoint>();
            _perThreadTraces = new Dictionary<int, List<FullStateTraceEvent>>();

            // Initialize 16 thread indices
            for (int i = 0; i < 16; i++)
            {
                _perThreadEventIndex[i] = 0;
                _perThreadTraces[i] = new List<FullStateTraceEvent>();
            }

            if (!string.IsNullOrEmpty(traceFilePath) && File.Exists(traceFilePath))
            {
                LoadBinaryTrace(traceFilePath);
            }
        }

        /// <summary>
        /// Load trace from TraceSink object
        /// </summary>
        public void LoadFromTraceSink(TraceSink traceSink)
        {
            _trace = traceSink;

            // Load per-thread traces
            for (int threadId = 0; threadId < 16; threadId++)
            {
                var threadTrace = traceSink.GetThreadTrace(threadId);
                _perThreadTraces[threadId] = new List<FullStateTraceEvent>(threadTrace);
            }
        }

        /// <summary>
        /// Load binary trace file
        /// </summary>
        private void LoadBinaryTrace(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open);
            using var reader = new BinaryReader(fs);

            // Read header
            uint magic = reader.ReadUInt32();
            if (magic != 0x54524143) // "TRAC"
            {
                throw new InvalidDataException("Invalid trace file magic number");
            }

            ushort version = reader.ReadUInt16();
            if (!IsSupportedBinaryTraceVersion(version))
            {
                throw new InvalidDataException($"Unsupported trace version: {version}");
            }

            int threadCount = reader.ReadInt32();

            // Read checkpoints
            int checkpointCount = reader.ReadInt32();
            for (int i = 0; i < checkpointCount; i++)
            {
                _checkpoints.Add(DeserializeCheckpoint(reader));
            }

            // Read per-thread traces
            for (int i = 0; i < threadCount; i++)
            {
                int threadId = reader.ReadInt32();
                int eventCount = reader.ReadInt32();

                if (!_perThreadTraces.ContainsKey(threadId))
                {
                    _perThreadTraces[threadId] = new List<FullStateTraceEvent>();
                }

                for (int j = 0; j < eventCount; j++)
                {
                    _perThreadTraces[threadId].Add(DeserializeFullStateEvent(reader, version));
                }
            }
        }

        /// <summary>
        /// Deserialize checkpoint from binary format
        /// </summary>
        private StateCheckpoint DeserializeCheckpoint(BinaryReader reader)
        {
            var checkpoint = new StateCheckpoint
            {
                CycleNumber = reader.ReadInt64()
            };

            int procStateLen = reader.ReadInt32();
            if (procStateLen > 0)
            {
                checkpoint.ProcessorState = reader.ReadBytes(procStateLen);
            }
            else
            {
                checkpoint.ProcessorState = Array.Empty<byte>();
            }

            int memSnapLen = reader.ReadInt32();
            if (memSnapLen > 0)
            {
                checkpoint.MemorySnapshot = reader.ReadBytes(memSnapLen);
            }
            else
            {
                checkpoint.MemorySnapshot = Array.Empty<byte>();
            }

            checkpoint.RandomState = reader.ReadUInt64();

            return checkpoint;
        }

        /// <summary>
        /// Deserialize full state event from binary format
        /// </summary>
        private FullStateTraceEvent DeserializeFullStateEvent(BinaryReader reader, ushort version)
        {
            var evt = new FullStateTraceEvent
            {
                PC = reader.ReadInt64(),
                BundleId = reader.ReadInt32(),
                OpIndex = reader.ReadInt32(),
                Opcode = reader.ReadUInt32(),
                ThreadId = reader.ReadInt32(),
                CycleNumber = reader.ReadInt64()
            };

            // Register file
            int regFileLen = reader.ReadInt32();
            if (regFileLen > 0)
            {
                evt.RegisterFile = new ulong[regFileLen];
                for (int i = 0; i < regFileLen; i++)
                {
                    evt.RegisterFile[i] = reader.ReadUInt64();
                }
            }

            // Vector registers
            int vregLen = reader.ReadInt32();
            if (vregLen > 0)
            {
                evt.VectorRegisters = new ulong[vregLen];
                for (int i = 0; i < vregLen; i++)
                {
                    evt.VectorRegisters[i] = reader.ReadUInt64();
                }
            }

            // Predicate registers
            int pregLen = reader.ReadInt32();
            if (pregLen > 0)
            {
                evt.PredicateRegisters = new ushort[pregLen];
                for (int i = 0; i < pregLen; i++)
                {
                    evt.PredicateRegisters[i] = reader.ReadUInt16();
                }
            }

            // FP context
            evt.FPContext = new FPExceptionContext
            {
                RoundingMode = reader.ReadInt32(),
                FlushToZero = reader.ReadBoolean(),
                DefaultNaN = reader.ReadBoolean()
            };

            // Memory writes
            int memWriteCount = reader.ReadInt32();
            if (memWriteCount > 0)
            {
                evt.MemoryWrites = new MemoryDelta[memWriteCount];
                for (int i = 0; i < memWriteCount; i++)
                {
                    ulong addr = reader.ReadUInt64();
                    int oldLen = reader.ReadInt32();
                    byte[] oldData = oldLen > 0 ? reader.ReadBytes(oldLen) : Array.Empty<byte>();
                    int newLen = reader.ReadInt32();
                    byte[] newData = newLen > 0 ? reader.ReadBytes(newLen) : Array.Empty<byte>();
                    evt.MemoryWrites[i] = new MemoryDelta(addr, oldData, newData);
                }
            }

            // FSP state
            evt.WasStolenSlot = reader.ReadBoolean();
            evt.OriginalThreadId = reader.ReadInt32();

            // Pipeline state
            evt.PipelineStage = reader.ReadString();
            evt.Stalled = reader.ReadBoolean();
            evt.StallReason = reader.ReadString();
            if (version >= 23)
            {
                evt.DecodedBundleStateOwnerKind = (DecodedBundleStateOwnerKind)reader.ReadByte();
                evt.DecodedBundleStateEpoch = reader.ReadUInt64();
                evt.DecodedBundleStateVersion = reader.ReadUInt64();
            }
            if (version >= 22)
            {
                evt.DecodedBundleStateKind = (DecodedBundleStateKind)reader.ReadByte();
                evt.DecodedBundleStateOrigin = (DecodedBundleStateOrigin)reader.ReadByte();
                evt.DecodedBundlePc = reader.ReadUInt64();
                evt.DecodedBundleValidMask = reader.ReadByte();
                evt.DecodedBundleNopMask = reader.ReadByte();
                evt.DecodedBundleHasCanonicalDecode = reader.ReadBoolean();
                evt.DecodedBundleHasCanonicalLegality = reader.ReadBoolean();
                evt.DecodedBundleHasDecodeFault = reader.ReadBoolean();
            }

            // Memory subsystem
            int bankQueueLen = reader.ReadInt32();
            if (bankQueueLen > 0)
            {
                evt.BankQueueDepths = new int[bankQueueLen];
                for (int i = 0; i < bankQueueLen; i++)
                {
                    evt.BankQueueDepths[i] = reader.ReadInt32();
                }
            }
            evt.ActiveMemoryRequests = reader.ReadInt32();
            evt.MemorySubsystemCycle = reader.ReadInt64();

            // Scheduler state
            int threadQueueLen = reader.ReadInt32();
            if (threadQueueLen > 0)
            {
                evt.ThreadReadyQueueDepths = new int[threadQueueLen];
                for (int i = 0; i < threadQueueLen; i++)
                {
                    evt.ThreadReadyQueueDepths[i] = reader.ReadInt32();
                }
            }
            evt.CurrentFSPPolicy = reader.ReadString();

            if (version >= 2)
            {
                evt.ReplayEpochId = reader.ReadUInt64();
                evt.ReplayPhaseCachedPc = reader.ReadUInt64();
                evt.ReplayEpochLength = reader.ReadUInt64();
                evt.ReplayPhaseValidSlotCount = reader.ReadInt32();
                evt.StableDonorMask = reader.ReadByte();
                evt.ReplayInvalidationReason = (ReplayPhaseInvalidationReason)reader.ReadByte();
                evt.PhaseCertificateTemplateReusable = reader.ReadBoolean();
                evt.PhaseCertificateReadyHits = reader.ReadInt64();
                evt.PhaseCertificateReadyMisses = reader.ReadInt64();
                if (version >= 3)
                {
                    evt.EstimatedPhaseCertificateChecksSaved = reader.ReadInt64();
                }
                evt.PhaseCertificateInvalidations = reader.ReadInt64();
                evt.PhaseCertificateInvalidationReason = (ReplayPhaseInvalidationReason)reader.ReadByte();
                if (version >= 4)
                {
                    evt.DeterminismReferenceOpportunitySlots = reader.ReadInt64();
                    evt.DeterminismReplayEligibleSlots = reader.ReadInt64();
                    evt.DeterminismMaskedSlots = reader.ReadInt64();
                    evt.DeterminismEstimatedLostSlots = reader.ReadInt64();
                    evt.DeterminismConstrainedCycles = reader.ReadInt64();
                    evt.DomainIsolationProbeAttempts = reader.ReadInt64();
                    evt.DomainIsolationBlockedAttempts = reader.ReadInt64();
                    evt.DomainIsolationCrossDomainBlocks = reader.ReadInt64();
                    evt.DomainIsolationKernelToUserBlocks = reader.ReadInt64();
                }

                if (version >= 5)
                {
                    evt.EligibilityMaskedCycles = reader.ReadInt64();
                    evt.EligibilityMaskedReadyCandidates = reader.ReadInt64();
                    evt.LastEligibilityRequestedMask = reader.ReadByte();
                    evt.LastEligibilityNormalizedMask = reader.ReadByte();
                    evt.LastEligibilityReadyPortMask = reader.ReadByte();
                    evt.LastEligibilityVisibleReadyMask = reader.ReadByte();
                    evt.LastEligibilityMaskedReadyMask = reader.ReadByte();
                }

                if (version >= 6)
                {
                    evt.AssistNominations = reader.ReadInt64();
                    evt.AssistInjections = reader.ReadInt64();
                    evt.AssistRejects = reader.ReadInt64();
                    evt.AssistBoundaryRejects = reader.ReadInt64();
                    evt.AssistInvalidations = reader.ReadInt64();
                    evt.AssistInterCoreNominations = reader.ReadInt64();
                    evt.AssistInterCoreInjections = reader.ReadInt64();
                    evt.AssistInterCoreRejects = reader.ReadInt64();
                    evt.AssistInterCoreDomainRejects = reader.ReadInt64();
                    if (version >= 7)
                    {
                        evt.AssistInterCorePodLocalInjections = reader.ReadInt64();
                        evt.AssistInterCoreCrossPodInjections = reader.ReadInt64();
                        evt.AssistInterCorePodLocalRejects = reader.ReadInt64();
                        evt.AssistInterCoreCrossPodRejects = reader.ReadInt64();
                        evt.AssistInterCorePodLocalDomainRejects = reader.ReadInt64();
                        evt.AssistInterCoreCrossPodDomainRejects = reader.ReadInt64();
                        if (version >= 8)
                        {
                            evt.AssistInterCoreSameVtVectorInjects = reader.ReadInt64();
                            evt.AssistInterCoreDonorVtVectorInjects = reader.ReadInt64();
                            if (version >= 13)
                            {
                                evt.AssistInterCoreSameVtVectorWritebackInjects = reader.ReadInt64();
                                evt.AssistInterCoreDonorVtVectorWritebackInjects = reader.ReadInt64();
                            }
                            if (version >= 9)
                            {
                                if (version >= 11 && version < 18)
                                {
                                    reader.ReadInt64();
                                }
                                if (version >= 17)
                                {
                                    evt.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects = reader.ReadInt64();
                                }
                                if (version >= 14)
                                {
                                    if (version >= 19)
                                    {
                                        evt.AssistInterCoreLane6HotLoadDonorPrefetchInjects = reader.ReadInt64();
                                    }
                                    else
                                    {
                                        reader.ReadInt64();
                                    }
                                }
                                if (version >= 15)
                                {
                                    evt.AssistInterCoreLane6HotStoreDonorPrefetchInjects = reader.ReadInt64();
                                }
                                if (version >= 12 && version < 18)
                                {
                                    reader.ReadInt64();
                                }
                                if (version >= 10)
                                {
                                    evt.AssistInterCoreLane6DonorPrefetchInjects = reader.ReadInt64();
                                }
                                if (version >= 16)
                                {
                                    evt.AssistInterCoreLane6ColdStoreLdsaInjects = reader.ReadInt64();
                                }
                                evt.AssistInterCoreLane6LdsaInjects = reader.ReadInt64();

                            }
                        }
                    }
                    evt.AssistQuotaRejects = reader.ReadInt64();
                    evt.AssistQuotaIssueRejects = reader.ReadInt64();
                    evt.AssistQuotaLineRejects = reader.ReadInt64();
                    evt.AssistQuotaLinesReserved = reader.ReadInt64();
                    evt.AssistBackpressureRejects = reader.ReadInt64();
                    evt.AssistBackpressureOuterCapRejects = reader.ReadInt64();
                    evt.AssistBackpressureMshrRejects = reader.ReadInt64();
                    evt.AssistBackpressureDmaSrfRejects = reader.ReadInt64();
                    evt.AssistDonorPrefetchInjects = reader.ReadInt64();
                    evt.AssistLdsaInjects = reader.ReadInt64();
                    evt.AssistVdsaInjects = reader.ReadInt64();
                    evt.AssistSameVtInjects = reader.ReadInt64();
                    evt.AssistDonorVtInjects = reader.ReadInt64();
                    evt.AssistInvalidationReason = (AssistInvalidationReason)reader.ReadByte();
                    evt.AssistOwnershipSignature = reader.ReadUInt64();
                }
            }

            return evt;
        }

        /// <summary>
        /// Find nearest checkpoint before or at target cycle
        /// </summary>
        private StateCheckpoint? FindNearestCheckpoint(long targetCycle)
        {
            if (_checkpoints.Count == 0)
                return null;

            // Find the checkpoint with the highest cycle number <= targetCycle
            StateCheckpoint? nearest = null;
            foreach (var checkpoint in _checkpoints)
            {
                if (checkpoint.CycleNumber <= targetCycle)
                {
                    if (nearest == null || checkpoint.CycleNumber > nearest.Value.CycleNumber)
                    {
                        nearest = checkpoint;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get events for a specific cycle across all threads
        /// </summary>
        private List<FullStateTraceEvent> GetEventsForCycle(long cycle)
        {
            var events = new List<FullStateTraceEvent>();

            foreach (var threadTrace in _perThreadTraces.Values)
            {
                foreach (var evt in threadTrace)
                {
                    if (evt.CycleNumber == cycle)
                    {
                        events.Add(evt);
                    }
                }
            }

            return events;
        }

        /// <summary>
        /// Replay execution up to specific cycle
        /// NOTE: This is a simplified implementation that would need integration
        /// with actual Processor struct for full replay capability
        /// </summary>
        public bool ReplayToCycle(long targetCycle, Action<FullStateTraceEvent>? eventCallback = null)
        {
            // 1. Find nearest checkpoint before target
            var checkpoint = FindNearestCheckpoint(targetCycle);
            long startCycle = checkpoint?.CycleNumber ?? 0;

            // 2. Replay from checkpoint (or beginning) to target
            long currentCycle = startCycle;

            while (currentCycle <= targetCycle)
            {
                // Get events for this cycle across all threads
                var cycleEvents = GetEventsForCycle(currentCycle);

                // Replay events in deterministic order (by thread ID, then op index)
                foreach (var evt in cycleEvents.OrderBy(e => e.ThreadId).ThenBy(e => e.OpIndex))
                {
                    // Call callback if provided
                    eventCallback?.Invoke(evt);

                    // In full implementation, would restore processor state here
                }

                currentCycle++;
            }

            return true;
        }

        /// <summary>
        /// Get trace statistics
        /// </summary>
        public (int TotalEvents, int ThreadsWithEvents, long MaxCycle) GetStatistics()
        {
            int totalEvents = 0;
            int threadsWithEvents = 0;
            long maxCycle = 0;

            foreach (var threadTrace in _perThreadTraces.Values)
            {
                if (threadTrace.Count > 0)
                {
                    threadsWithEvents++;
                    totalEvents += threadTrace.Count;

                    long threadMaxCycle = threadTrace.Max(e => e.CycleNumber);
                    if (threadMaxCycle > maxCycle)
                    {
                        maxCycle = threadMaxCycle;
                    }
                }
            }

            return (totalEvents, threadsWithEvents, maxCycle);
        }

        /// <summary>
        /// Verify trace integrity
        /// </summary>
        public bool VerifyTraceIntegrity()
        {
            // Check that all thread traces are ordered by cycle number
            foreach (var kvp in _perThreadTraces)
            {
                var trace = kvp.Value;
                for (int i = 1; i < trace.Count; i++)
                {
                    if (trace[i].CycleNumber < trace[i - 1].CycleNumber)
                    {
                        return false; // Not monotonically increasing
                    }
                }
            }

            return VerifyReplayPhaseConsistency();
        }

        /// <summary>
        /// Verify replay-phase metadata consistency across the captured trace.
        /// </summary>
        public bool VerifyReplayPhaseConsistency()
        {
            foreach (var trace in _perThreadTraces.Values)
            {
                ulong currentEpochId = 0;
                ulong cachedPc = 0;
                ulong epochLength = 0;
                int validSlotCount = 0;
                byte donorMask = 0;

                foreach (var evt in trace)
                {
                    if (evt.ReplayEpochId == 0)
                    {
                        if (evt.ReplayEpochLength != 0 || evt.StableDonorMask != 0)
                            return false;

                        continue;
                    }

                    if (currentEpochId != evt.ReplayEpochId)
                    {
                        currentEpochId = evt.ReplayEpochId;
                        cachedPc = evt.ReplayPhaseCachedPc;
                        epochLength = evt.ReplayEpochLength;
                        validSlotCount = evt.ReplayPhaseValidSlotCount;
                        donorMask = evt.StableDonorMask;
                    }
                    else if (cachedPc != evt.ReplayPhaseCachedPc ||
                             epochLength != evt.ReplayEpochLength ||
                             validSlotCount != evt.ReplayPhaseValidSlotCount ||
                             donorMask != evt.StableDonorMask)
                    {
                        return false;
                    }

                    if (evt.PhaseCertificateTemplateReusable &&
                        evt.PhaseCertificateInvalidationReason == ReplayPhaseInvalidationReason.CertificateMutation)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Compare two repeated runs and report whether replay-heavy behavior stayed deterministic.
        /// </summary>
        public static ReplayDeterminismReport CompareRepeatedRuns(TraceSink baselineTrace, TraceSink candidateTrace)
        {
            ArgumentNullException.ThrowIfNull(baselineTrace);
            ArgumentNullException.ThrowIfNull(candidateTrace);

            var baselineReplay = new ReplayEngine();
            baselineReplay.LoadFromTraceSink(baselineTrace);

            var candidateReplay = new ReplayEngine();
            candidateReplay.LoadFromTraceSink(candidateTrace);

            return baselineReplay.CompareReplayPhaseBehavior(candidateReplay);
        }

        /// <summary>
        /// Compare two repeated runs while tolerating bounded timing/resource perturbations.
        /// </summary>
        public static ReplayEnvelopeReport CompareRepeatedRunsWithinEnvelope(
            TraceSink baselineTrace,
            TraceSink candidateTrace,
            ReplayEnvelopeConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(baselineTrace);
            ArgumentNullException.ThrowIfNull(candidateTrace);

            var baselineReplay = new ReplayEngine();
            baselineReplay.LoadFromTraceSink(baselineTrace);

            var candidateReplay = new ReplayEngine();
            candidateReplay.LoadFromTraceSink(candidateTrace);

            return baselineReplay.CompareReplayPhaseBehaviorWithinEnvelope(candidateReplay, configuration);
        }

        /// <summary>
        /// Summarize replay-heavy evidence directly from a trace sink.
        /// </summary>
        public static ReplayTraceEvidenceSummary SummarizeReplayPhaseEvidence(TraceSink traceSink)
        {
            ArgumentNullException.ThrowIfNull(traceSink);

            var replay = new ReplayEngine();
            replay.LoadFromTraceSink(traceSink);
            return replay.SummarizeReplayPhaseEvidence();
        }

        /// <summary>
        /// Summarize per-epoch replay-heavy evidence directly from a trace sink.
        /// </summary>
        public static ReplayEpochEvidenceSummary[] SummarizeReplayEpochEvidence(TraceSink traceSink)
        {
            ArgumentNullException.ThrowIfNull(traceSink);

            var replay = new ReplayEngine();
            replay.LoadFromTraceSink(traceSink);
            return replay.SummarizeReplayEpochEvidence();
        }

        /// <summary>
        /// Compare replay-phase behavior between this run and another run.
        /// </summary>
        public ReplayDeterminismReport CompareReplayPhaseBehavior(ReplayEngine other)
        {
            ArgumentNullException.ThrowIfNull(other);

            int comparedEvents = 0;
            int comparedReplayEvents = 0;
            int comparedTimelineSamples = 0;
            int comparedInvalidationEvents = 0;
            int comparedEpochs = 0;

            for (int threadId = 0; threadId < 16; threadId++)
            {
                var baselineTrace = _perThreadTraces[threadId];
                var candidateTrace = other._perThreadTraces[threadId];

                if (baselineTrace.Count != candidateTrace.Count)
                {
                    return new ReplayDeterminismReport(
                        false,
                        comparedEvents,
                        comparedReplayEvents,
                        comparedTimelineSamples,
                        comparedInvalidationEvents,
                        comparedEpochs,
                        threadId,
                        -1,
                        "EventCount",
                        baselineTrace.Count.ToString(),
                        candidateTrace.Count.ToString());
                }

                ulong lastEpochId = 0;
                for (int index = 0; index < baselineTrace.Count; index++)
                {
                    FullStateTraceEvent baselineEvent = baselineTrace[index];
                    FullStateTraceEvent candidateEvent = candidateTrace[index];

                    if (!TryCompareReplayEvent(baselineEvent, candidateEvent, out string mismatchField, out string expectedValue, out string actualValue))
                    {
                        return new ReplayDeterminismReport(
                            false,
                            comparedEvents,
                            comparedReplayEvents,
                            comparedTimelineSamples,
                            comparedInvalidationEvents,
                            comparedEpochs,
                            threadId,
                            baselineEvent.CycleNumber,
                            mismatchField,
                            expectedValue,
                            actualValue);
                    }

                    comparedEvents++;

                    if (baselineEvent.ReplayEpochId != 0)
                    {
                        comparedReplayEvents++;
                        if (baselineEvent.ReplayEpochId != lastEpochId)
                        {
                            comparedEpochs++;
                            lastEpochId = baselineEvent.ReplayEpochId;
                        }
                    }

                    if (IsDensePhaseTimelineSample(baselineEvent))
                    {
                        comparedTimelineSamples++;
                    }

                    if (HasInvalidationSignal(baselineEvent))
                    {
                        comparedInvalidationEvents++;
                    }
                }
            }

            return new ReplayDeterminismReport(
                true,
                comparedEvents,
                comparedReplayEvents,
                comparedTimelineSamples,
                comparedInvalidationEvents,
                comparedEpochs,
                -1,
                -1,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        /// <summary>
        /// Compare replay-phase behavior while tolerating bounded timing/resource perturbations.
    }
}
