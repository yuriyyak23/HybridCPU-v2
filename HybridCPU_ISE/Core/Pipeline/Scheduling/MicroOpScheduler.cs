using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Scoreboard entry type for distinguishing DMA from Outstanding Load/Store entries.
    /// HLS: 2-bit enum → 2 flip-flops per entry.
    /// </summary>
    public enum ScoreboardEntryType : byte
    {
        /// <summary>Slot is free (no pending operation).</summary>
        Free = 0,

        /// <summary>DMA transaction pending (legacy behavior).</summary>
        Dma = 1,

        /// <summary>Outstanding Load pending (MSHR tracking).</summary>
        OutstandingLoad = 2,

        /// <summary>Outstanding Store pending (store-buffer tracking).</summary>
        OutstandingStore = 3
    }

    /// <summary>
    /// Micro-operation scheduler for Formally Safe Packing (FSP).
    ///
    /// Manages 16 nomination ports (one per core in a Pod), a priority encoder
    /// for slot stealing, and per-VT scoreboards for DMA/MSHR tracking.
    ///
    /// HLS design constraints:
    /// - Fixed-size arrays only (no dynamic allocation, no List/Dictionary)
    /// - Deterministic iteration order (ascending core ID = priority encoder)
    /// - All state fits in register file / LUTRAM (no BRAM for control path)
    /// - Single-cycle combinational paths for Nominate / TryStealSlot
    ///
    /// Refactoring Pt. 1 integration:
    /// - globalHardwareMask (from MemorySubsystem.GetHardwareOccupancyMask128)
    ///   is merged into legality checker conflict checks during PackBundle,
    ///   replacing the ad-hoc SuppressLsu boolean bypass.
    ///
    /// Refactoring Pt. 3 integration:
    /// - Universal Scoreboard: per-VT type-tagged entries track DMA, Outstanding Loads
    ///   and Outstanding Stores. Memory bank IDs in scoreboard allow FSP and pipeline
    ///   ID-stage to detect bank-level conflicts, preventing Load/Store injection when
    ///   the target bank has in-flight MSHR entries for the same virtual thread.
    /// </summary>
    public partial class MicroOpScheduler : ILegalityCertificateCacheTelemetrySink
    {
        // ── TDM (Time-Division Multiplexing) ─────────────────────────

        /// <summary>Global cycle counter for TDM arbitration.</summary>
        public ulong GlobalCycleCounter = 0;

        /// <summary>TDM period config (N-th cycle dedicated to background thread).</summary>
        public const int TDM_PERIOD = 64;

        // ── Constants ────────────────────────────────────────────────

        /// <summary>Number of nomination ports (one per core in a Pod).</summary>
        private const int NUM_PORTS = 16;

        /// <summary>Number of SMT virtual threads per physical core.</summary>
        private const int SMT_WAYS = 4;

        /// <summary>Number of scoreboard slots per virtual thread.</summary>
        private const int SCOREBOARD_SLOTS = 8;

        // ── Nomination Ports ─────────────────────────────────────────

        /// <summary>
        /// Live nomination ports: each core writes its candidate here.
        /// HLS: 16-entry register file, written by core logic, read by arbiter.
        /// </summary>
        private readonly MicroOp?[] _ports = new MicroOp?[NUM_PORTS];

        /// <summary>
        /// Port validity flags (true = port has a valid candidate).
        /// Separate from null check for HLS: 16-bit register, 1 bit per core.
        /// </summary>
        private readonly bool[] _portValid = new bool[NUM_PORTS];

        /// <summary>
        /// Latched ready mask from the previous cycle's visible inter-core nominations.
        /// Consumers that only need donor visibility read this transport rather than
        /// depending on shadow copies of the raw nomination port arrays.
        /// HLS: 16-bit pipeline register.
        /// </summary>
        private ushort _latchedInterCoreNominationReadyMask;

        /// <summary>
        /// Per-core stall flags. When a core is stalled its nominations are annulled.
        /// HLS: 16-bit register.
        /// </summary>
        private readonly bool[] _coreStalled = new bool[NUM_PORTS];

        // ── SMT Nomination Ports (Intra-Core 4-Way) ──────────────────

        /// <summary>
        /// Per-virtual-thread SMT nomination ports for intra-core scheduling.
        /// HLS: 4-entry register file, indexed by VirtualThreadId.
        /// </summary>
        private readonly MicroOp?[] _smtPorts = new MicroOp?[SMT_WAYS];
        private readonly bool[] _smtPortValid = new bool[SMT_WAYS];

        // ── Phase 2A: Credit-Based Deterministic Fairness ────────────

        /// <summary>
        /// Maximum credit a VT can accumulate before capping.
        /// HLS: constant, compiled into comparator threshold.
        /// </summary>
        private const int FAIRNESS_CREDIT_CAP = 16;
        private const int LoopPhaseActivationThreshold = 8;
        private const int LoopPhaseSampleCapacity = 16;
        private const int LoopPhaseExportLimit = 8;
        private const int LoopPhaseHeatPruneThreshold = 32;

        /// <summary>
        /// Per-VT fairness credit counters. Each cycle without injection
        /// accumulates credit; each successful injection spends one credit.
        /// Ranking uses descending credit order (highest credit = first chance).
        /// Tie-break: ascending vtId (deterministic).
        /// HLS: 4 × 16-bit register file = 64 bits → one LUTRAM.
        /// </summary>
        private readonly int[] _fairnessCredits = new int[SMT_WAYS];

        /// <summary>
        /// Per-VT successful injection counter for fairness telemetry.
        /// HLS: 4 × 64-bit counters (diagnostic only, not in critical path).
        /// </summary>
        private readonly long[] _perVtInjections = new long[SMT_WAYS];

        /// <summary>Number of starvation events detected (VT had valid candidate but was skipped for >FAIRNESS_CREDIT_CAP consecutive cycles).</summary>
        public long FairnessStarvationEvents { get; private set; }

        /// <summary>Whether credit-based fairness ranking is enabled. Default: true (Phase 2).</summary>
        public bool CreditFairnessEnabled { get; set; } = true;

        /// <summary>
        /// Enables per-loop replay/class-capacity sampling for compiler-facing telemetry export.
        /// Default is <see langword="false"/> so existing replay paths stay unchanged unless explicitly opted in.
        /// </summary>
        public bool EnableLoopPhaseSampling { get; set; }

        // ── Phase 2B: Bank-Pressure-Aware FSP ────────────────────────

        /// <summary>Number of injection decisions where bank-pressure tie-break selected a lower-pressure candidate.</summary>
        public long BankPressureAvoidanceCount { get; private set; }

        /// <summary>Whether bank-pressure tie-breaking is enabled for memory-op candidates. Default: true (Phase 2).</summary>
        public bool BankPressureTieBreakEnabled { get; set; } = true;

        // ── Phase 2C: Speculation Budget ─────────────────────────────

        /// <summary>
        /// Maximum number of concurrent speculative operations allowed.
        /// HLS: 4-bit constant → comparator threshold.
        /// </summary>
        public int SpeculationBudgetMax { get; set; } = SCOREBOARD_SLOTS / 2;

        /// <summary>
        /// Current speculation budget (decremented on speculative inject, incremented on commit/squash).
        /// HLS: 4-bit register → 4 flip-flops.
        /// </summary>
        private int _speculationBudget;

        /// <summary>Number of speculative injections blocked because budget was exhausted.</summary>
        public long SpeculationBudgetExhaustionEvents { get; private set; }

        /// <summary>Peak number of concurrent speculative operations observed.</summary>
        public long PeakConcurrentSpeculativeOps { get; private set; }

        /// <summary>Whether speculation budget enforcement is enabled. Default: true (Phase 2).</summary>
        public bool SpeculationBudgetEnabled { get; set; } = true;

        // ── Scoreboard (DMA / Outstanding Loads / Outstanding Stores) ──

        /// <summary>
        /// Global scoreboard: tracks pending targets (register IDs or DMA channels).
        /// _scoreboard[slot] = targetId that is pending, or -1 if slot is free.
        /// HLS: 8-entry register file (8 × 16-bit target ID + 8 × 1-bit valid).
        /// </summary>
        private readonly int[] _scoreboard = new int[SCOREBOARD_SLOTS];

        /// <summary>
        /// Per-VT scoreboard for 4-way SMT isolation.
        /// _smtScoreboard[vt, slot] = targetId pending for that VT, or -1 if free.
        /// HLS: 4 × 8-entry register file = 32 entries total.
        /// </summary>
        private readonly int[,] _smtScoreboard = new int[SMT_WAYS, SCOREBOARD_SLOTS];

        /// <summary>
        /// Per-VT scoreboard entry type (Refactoring Pt. 3: Universal Scoreboard).
        /// _smtScoreboardType[vt, slot] = entry classification (Free/Dma/OutstandingLoad/OutstandingStore).
        /// HLS: 4 × 8 × 2-bit register file = 64 bits total.
        /// </summary>
        private readonly ScoreboardEntryType[,] _smtScoreboardType = new ScoreboardEntryType[SMT_WAYS, SCOREBOARD_SLOTS];

        /// <summary>
        /// Per-VT scoreboard bank ID (Refactoring Pt. 3: bank-level conflict detection).
        /// _smtScoreboardBankId[vt, slot] = memory bank ID (0–15) for Load/Store entries, or -1 if N/A.
        /// HLS: 4 × 8 × 5-bit register file (4-bit bank ID + 1 valid bit) = 160 bits total.
        /// </summary>
        private readonly int[,] _smtScoreboardBankId = new int[SMT_WAYS, SCOREBOARD_SLOTS];

        // Sampled hardware-occupancy view for the current packing pass.
        // The structural mask remains on the existing legality path, while the explicit
        // memory-budget state closes the widened lane4..5 LSU load/store contract.
        // A shared mixed ceiling remains as the conservative compatibility guardrail,
        // while split load/store credits now follow the memory-side turnaround model.
        // Projected outstanding-memory state keeps pack-time per-VT saturation aligned
        // with the real ID→EX scoreboard allocator, including pre-existing DMA occupancy.
        private HardwareOccupancySnapshot128 _hardwareOccupancySnapshot =
            HardwareOccupancySnapshot128.Permissive;
        private byte _remainingHardwareMemoryIssueBudget = 2;
        private uint _consumedHardwareMemoryBudgetByBank;
        private byte _remainingHardwareLoadIssueBudget = 2;
        private byte _remainingHardwareStoreIssueBudget = 2;
        private uint _consumedHardwareLoadBudgetByBank;
        private uint _consumedHardwareStoreBudgetByBank;
        private ushort _bundleLocalOutstandingStoreBankMask;
        private readonly byte[] _projectedOutstandingMemoryCountByVt = new byte[SMT_WAYS];
        private readonly byte[] _projectedOutstandingMemoryCapacityByVt = new byte[SMT_WAYS];

        // ── 2-Stage Pipelined FSP (HLS Timing Closure §1) ───────────

        /// <summary>
        /// FSP pipeline stage enum for 2-stage arbitration.
        /// SCHED1: Nomination and source capture.
        /// SCHED2: Intersection &amp; priority-encoded commit.
        /// HLS: 1-bit flip-flop per scheduler instance.
        /// </summary>
        public enum FspPipelineStage : byte
        {
            /// <summary>Cycle 1: Capture viable donor VT identities from SMT ports.</summary>
            SCHED1 = 0,

            /// <summary>Cycle 2: Intersect masks, priority-encode, commit to scoreboard.</summary>
            SCHED2 = 1
        }

        /// <summary>
        /// Pipeline register entry between SCHED1 and SCHED2.
        /// Stores only stable source identity so SCHED2 can reload the live
        /// candidate from <c>_smtPorts</c> without maintaining shadow mask or placement metadata.
        /// HLS: 4 entries × (128-bit mask + 1-bit valid + metadata) ≈ 600 flip-flops.
        /// </summary>
        private struct FspPipelineRegister
        {
            /// <summary>True if this register holds a valid candidate.</summary>
            public bool Valid;

            /// <summary>Virtual thread ID of the candidate (0–3).</summary>
            public int VirtualThreadId;

            /// <summary>Memory bank ID if candidate is Load/Store, -1 otherwise.</summary>

            /// <summary>True if candidate is a memory operation (Load/Store).</summary>

            // Phase 06: typed-slot metadata (3-bit class + 1-bit pinning + 3-bit lane = 7 flip-flops per entry)

            /// <summary>Required slot class of the candidate (Phase 01 taxonomy).</summary>

            /// <summary>Pinning kind of the candidate (ClassFlexible or HardPinned).</summary>

            /// <summary>Pinned lane ID for HardPinned candidates (0–7), 0 for ClassFlexible.</summary>
        }

        private sealed class LoopPhaseTracker
        {
            private readonly ClassCapacityTemplate[] _samples = new ClassCapacityTemplate[MicroOpScheduler.LoopPhaseSampleCapacity];
            private int _sampleCount;
            private int _nextSampleIndex;

            public LoopPhaseTracker(ulong loopPcAddress, int phaseEntryCount)
            {
                LoopPcAddress = loopPcAddress;
                PhaseEntryCount = phaseEntryCount;
            }

            public ulong LoopPcAddress { get; }

            public int PhaseEntryCount { get; private set; }

            public int IterationsSampled => _sampleCount;

            public void SetPhaseEntryCount(int phaseEntryCount)
            {
                PhaseEntryCount = phaseEntryCount;
            }

            public void AddSample(ClassCapacityTemplate template)
            {
                _samples[_nextSampleIndex] = template;
                if (_sampleCount < MicroOpScheduler.LoopPhaseSampleCapacity)
                {
                    _sampleCount++;
                }

                _nextSampleIndex = (_nextSampleIndex + 1) % MicroOpScheduler.LoopPhaseSampleCapacity;
            }

            public LoopPhaseClassProfile BuildProfile()
            {
                if (_sampleCount <= 0)
                {
                    return new LoopPhaseClassProfile(
                        LoopPcAddress,
                        0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0);
                }

                double aluMean = 0.0;
                double lsuMean = 0.0;
                double dmaMean = 0.0;
                double branchMean = 0.0;
                double systemMean = 0.0;
                int templateReuseMatches = 0;
                ClassCapacityTemplate? previous = null;

                for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
                {
                    ClassCapacityTemplate sample = GetOrderedSample(sampleIndex);
                    aluMean += sample.AluFree;
                    lsuMean += sample.LsuFree;
                    dmaMean += sample.DmaStreamFree;
                    branchMean += sample.BranchControlFree;
                    systemMean += sample.SystemSingletonFree;

                    if (previous is ClassCapacityTemplate previousSample && previousSample.Equals(sample))
                    {
                        templateReuseMatches++;
                    }

                    previous = sample;
                }

                double sampleCount = _sampleCount;
                aluMean /= sampleCount;
                lsuMean /= sampleCount;
                dmaMean /= sampleCount;
                branchMean /= sampleCount;
                systemMean /= sampleCount;

                double aluVariance = 0.0;
                double lsuVariance = 0.0;
                double dmaVariance = 0.0;
                double branchVariance = 0.0;
                double systemVariance = 0.0;

                for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
                {
                    ClassCapacityTemplate sample = GetOrderedSample(sampleIndex);
                    aluVariance += Square(sample.AluFree - aluMean);
                    lsuVariance += Square(sample.LsuFree - lsuMean);
                    dmaVariance += Square(sample.DmaStreamFree - dmaMean);
                    branchVariance += Square(sample.BranchControlFree - branchMean);
                    systemVariance += Square(sample.SystemSingletonFree - systemMean);
                }

                aluVariance /= sampleCount;
                lsuVariance /= sampleCount;
                dmaVariance /= sampleCount;
                branchVariance /= sampleCount;
                systemVariance /= sampleCount;

                double overallVariance = (aluVariance + lsuVariance + dmaVariance + branchVariance + systemVariance) / 5.0;
                double templateReuseRate = _sampleCount > 1
                    ? (double)templateReuseMatches / (_sampleCount - 1)
                    : 0.0;

                return new LoopPhaseClassProfile(
                    LoopPcAddress,
                    _sampleCount,
                    aluVariance,
                    lsuVariance,
                    dmaVariance,
                    branchVariance,
                    systemVariance,
                    overallVariance,
                    templateReuseRate);
            }

            private ClassCapacityTemplate GetOrderedSample(int sampleIndex)
            {
                int oldestIndex = _sampleCount == MicroOpScheduler.LoopPhaseSampleCapacity ? _nextSampleIndex : 0;
                int actualIndex = (oldestIndex + sampleIndex) % MicroOpScheduler.LoopPhaseSampleCapacity;
                return _samples[actualIndex];
            }

            private static double Square(double value) => value * value;
        }

        /// <summary>
        /// SCHED1→SCHED2 pipeline register bank: one entry per SMT way.
        /// Written by PipelineFspStage1_Nominate, read by PipelineFspStage2_Intersect.
        /// HLS: 4-entry register file, D-flip-flop bank (clocked on pipeline edge).
        /// </summary>
        private readonly FspPipelineRegister[] _fspPipelineReg = new FspPipelineRegister[SMT_WAYS];
        private readonly Dictionary<ulong, int> _loopPhaseEntryCounts = new();
        private readonly Dictionary<ulong, LoopPhaseTracker> _loopPhaseTrackers = new();

        private readonly struct BundleOpportunityState
        {
            public BundleOpportunityState(
                byte occupancyMask,
                byte emptySlotMask,
                byte referenceOpportunityMask)
            {
                OccupancyMask = occupancyMask;
                EmptySlotMask = emptySlotMask;
                ReferenceOpportunityMask = referenceOpportunityMask;
            }

            public byte OccupancyMask { get; }

            public byte EmptySlotMask { get; }

            public byte ReferenceOpportunityMask { get; }

            public int ReferenceOpportunityCount => BitOperations.PopCount((uint)ReferenceOpportunityMask);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CountReplayEligibleReferenceOpportunities(byte replayAwareMask)
            {
                return BitOperations.PopCount((uint)(ReferenceOpportunityMask & replayAwareMask));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsReplayEligibleReferenceOpportunity(int slotIndex, byte replayAwareMask)
            {
                if ((uint)slotIndex >= 8)
                    return false;

                byte slotBit = (byte)(1 << slotIndex);
                return (ReferenceOpportunityMask & slotBit) != 0 &&
                    (replayAwareMask & slotBit) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindNextInjectableEmptySlot(
                int startIndex,
                bool hasStableDonorStructure,
                byte stableDonorMask)
            {
                byte eligibleMask = hasStableDonorStructure
                    ? (byte)(EmptySlotMask & stableDonorMask)
                    : EmptySlotMask;
                return FindNextSlotInMask(eligibleMask, startIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BundleOpportunityState WithOccupiedSlot(int slotIndex)
            {
                if ((uint)slotIndex >= 8)
                    return this;

                byte slotBit = (byte)(1 << slotIndex);
                return new BundleOpportunityState(
                    (byte)(OccupancyMask | slotBit),
                    (byte)(EmptySlotMask & ~slotBit),
                    (byte)(ReferenceOpportunityMask & ~slotBit));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static BundleOpportunityState Create(IReadOnlyList<MicroOp?> bundle)
            {
                byte occupancyMask = 0;
                byte emptySlotMask = 0;
                byte referenceOpportunityMask = 0;

                int slotCount = Math.Min(bundle?.Count ?? 0, 8);
                for (int slotIndex = 0; slotIndex < 8; slotIndex++)
                {
                    MicroOp? slotOp = bundle is not null && slotIndex < slotCount
                        ? bundle[slotIndex]
                        : null;
                    byte slotBit = (byte)(1 << slotIndex);
                    if (slotOp is null)
                    {
                        emptySlotMask |= slotBit;
                        referenceOpportunityMask |= slotBit;
                        continue;
                    }

                    occupancyMask |= slotBit;
                    if (slotOp is NopMicroOp)
                    {
                        if (slotOp.AdmissionMetadata.IsStealable)
                        {
                            referenceOpportunityMask |= slotBit;
                        }
                    }
                }

                return new BundleOpportunityState(occupancyMask, emptySlotMask, referenceOpportunityMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int FindNextSlotInMask(byte slotMask, int startIndex)
            {
                for (int slotIndex = Math.Max(startIndex, 0); slotIndex < 8; slotIndex++)
                {
                    if ((slotMask & (1 << slotIndex)) != 0)
                        return slotIndex;
                }

                return -1;
            }
        }

        private readonly struct SmtNominationState
        {
            public SmtNominationState(byte readyMask)
            {
                ReadyMask = readyMask;
            }

            public byte ReadyMask { get; }

            public bool HasReadyBackgroundCandidates => (ReadyMask & 0b1110) != 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsReadyCandidate(int virtualThreadId)
            {
                if ((uint)virtualThreadId >= SMT_WAYS)
                    return false;

                return (ReadyMask & (1 << virtualThreadId)) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsReadyNonOwnerCandidate(int virtualThreadId, int ownerVirtualThreadId)
            {
                return virtualThreadId != ownerVirtualThreadId && IsReadyCandidate(virtualThreadId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CountReadyCandidatesExcluding(int ownerVirtualThreadId)
            {
                byte eligibleMask = ReadyMask;
                if ((uint)ownerVirtualThreadId < SMT_WAYS)
                {
                    eligibleMask = (byte)(eligibleMask & ~(1 << ownerVirtualThreadId));
                }

                return BitOperations.PopCount((uint)eligibleMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SmtNominationState WithoutCandidate(int virtualThreadId)
            {
                if ((uint)virtualThreadId >= SMT_WAYS)
                    return this;

                return new SmtNominationState((byte)(ReadyMask & ~(1 << virtualThreadId)));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static SmtNominationState Create(MicroOp?[] ports, bool[] validPorts)
            {
                byte readyMask = 0;
                for (int vt = 0; vt < SMT_WAYS; vt++)
                {
                    if (validPorts[vt] && ports[vt] is not null)
                    {
                        readyMask |= (byte)(1 << vt);
                    }
                }

                return new SmtNominationState(readyMask);
            }
        }

        private readonly struct InterCoreNominationSnapshot
        {
            private readonly MicroOp?[]? _candidates;

            public InterCoreNominationSnapshot(ushort readyMask, MicroOp?[] candidates)
            {
                ReadyMask = readyMask;
                _candidates = candidates;
            }

            public ushort ReadyMask { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsReadyCandidate(int coreId)
            {
                if ((uint)coreId >= NUM_PORTS)
                    return false;

                return ((ReadyMask >> coreId) & 1) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetCandidate(int coreId, out MicroOp candidate)
            {
                candidate = null!;
                MicroOp?[]? candidates = _candidates;
                if (candidates is null || !IsReadyCandidate(coreId))
                    return false;

                MicroOp? nominatedCandidate = candidates[coreId];
                if (nominatedCandidate is null)
                    return false;

                candidate = nominatedCandidate;
                return true;
            }

            public static InterCoreNominationSnapshot Capture(MicroOp?[] ports, bool[] validPorts)
            {
                ushort readyMask = 0;
                var candidates = new MicroOp?[NUM_PORTS];
                for (int coreId = 0; coreId < NUM_PORTS; coreId++)
                {
                    if (validPorts[coreId] && ports[coreId] is MicroOp candidate)
                    {
                        readyMask |= (ushort)(1 << coreId);
                        candidates[coreId] = candidate;
                    }
                }

                return new InterCoreNominationSnapshot(readyMask, candidates);
            }

            public static InterCoreNominationSnapshot Capture(Queue<MicroOp>[] nominationQueues)
            {
                ushort readyMask = 0;
                var candidates = new MicroOp?[NUM_PORTS];
                int visibleQueueCount = Math.Min(nominationQueues.Length, Math.Min(SMT_WAYS, NUM_PORTS));
                for (int queueId = 0; queueId < visibleQueueCount; queueId++)
                {
                    Queue<MicroOp> queue = nominationQueues[queueId];
                    if (queue.Count == 0)
                        continue;

                    readyMask |= (ushort)(1 << queueId);
                    candidates[queueId] = queue.Peek();
                }

                return new InterCoreNominationSnapshot(readyMask, candidates);
            }
        }

        private readonly struct SmtNominationSnapshot
        {
            private readonly MicroOp?[]? _candidates;

            public SmtNominationSnapshot(SmtNominationState nominationState, MicroOp?[] candidates)
            {
                NominationState = nominationState;
                _candidates = candidates;
            }

            public SmtNominationState NominationState { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetCandidate(int virtualThreadId, out MicroOp candidate)
            {
                candidate = null!;
                MicroOp?[]? candidates = _candidates;
                if (candidates is null || !NominationState.IsReadyCandidate(virtualThreadId))
                    return false;

                MicroOp? nominatedCandidate = candidates[virtualThreadId];
                if (nominatedCandidate is null)
                    return false;

                candidate = nominatedCandidate;
                return true;
            }

            public static SmtNominationSnapshot Capture(
                MicroOp?[] ports,
                SmtNominationState nominationState)
            {
                var candidates = new MicroOp?[SMT_WAYS];
                for (int vt = 0; vt < SMT_WAYS; vt++)
                {
                    if (nominationState.IsReadyCandidate(vt))
                    {
                        candidates[vt] = ports[vt];
                    }
                }

                return new SmtNominationSnapshot(nominationState, candidates);
            }
        }

        private struct ClassTemplateAdmissionState
        {
            private TemplateBudget _templateBudget;

            private ClassTemplateAdmissionState(bool templateHit, TemplateBudget templateBudget)
            {
                TemplateHit = templateHit;
                _templateBudget = templateBudget;
            }

            public bool TemplateHit { get; }

            public TemplateBudget TemplateBudgetSnapshot => _templateBudget;

            public static ClassTemplateAdmissionState Create(TemplateBudget templateBudget)
            {
                return new ClassTemplateAdmissionState(templateHit: true, templateBudget);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryConsumeFastPathBudget(SlotClass slotClass)
            {
                if (!TemplateHit)
                    return false;

                if (_templateBudget.GetRemaining(slotClass) <= 0)
                    return false;

                _templateBudget.Decrement(slotClass);
                return true;
            }
        }

        /// <summary>
        /// Current FSP pipeline stage (alternates each cycle when pipelined mode is active).
        /// </summary>
        private FspPipelineStage _fspCurrentStage = FspPipelineStage.SCHED1;
        private ulong _lastLoopPhaseEntryEpochId;
        private ulong _lastLoopPhaseEntryPc;
        private ulong _lastLoopPhaseSampleEpochId;
        private ulong _lastLoopPhaseSamplePc;

        /// <summary>
        /// Owner VT ID latched during SCHED1 for use in SCHED2 (Phase 03: TryClassAdmission).
        /// HLS: 2-bit register, clocked on SCHED1 edge.
        /// </summary>
        private int _fspOwnerVirtualThreadId;

        /// <summary>Number of cycles added by the 2-stage FSP pipeline (diagnostic).</summary>
        public long FspPipelineLatencyCycles { get; private set; }

        // ── Safety Verifier ──────────────────────────────────────────

        // ── Statistics ───────────────────────────────────────────────

        /// <summary>Number of PackBundle calls (one per scheduling cycle).</summary>
        public long TotalSchedulerCycles { get; private set; }

        /// <summary>Number of successfully injected (stolen) operations.</summary>
        public long SuccessfulInjectionsCount { get; private set; }

        /// <summary>Intra-core SMT successful injections.</summary>
        public long SmtInjectionsCount { get; private set; }

        /// <summary>Intra-core SMT rejections (safety mask conflict).</summary>
        public long SmtRejectionsCount { get; private set; }

        private ReplayPhaseContext _currentReplayPhase;

        internal ReplayPhaseContext CurrentReplayPhase => _currentReplayPhase;

        /// <summary>
        /// Runtime-local legality service for inter-core/SMT decision paths and adjacent diagnostics.
        /// </summary>
        private readonly IRuntimeLegalityService _runtimeLegalityService;

        /// <summary>Number of scheduling cycles executed while a replay phase was active.</summary>
        public long ReplayAwareCycles { get; private set; }

        /// <summary>Number of legality checks that could reuse a phase certificate template.</summary>
        public long PhaseCertificateReadyHits { get; private set; }

        /// <summary>Number of legality checks that had replay context but no reusable template.</summary>
        public long PhaseCertificateReadyMisses { get; private set; }

        /// <summary>Estimated number of repeated legality checks avoided by template reuse.</summary>
        public long EstimatedPhaseCertificateChecksSaved { get; private set; }

        /// <summary>Number of explicit phase-certificate invalidations.</summary>
        public long PhaseCertificateInvalidations { get; private set; }

        /// <summary>Number of phase-certificate invalidations caused by bundle mutation.</summary>
        public long PhaseCertificateMutationInvalidations { get; private set; }

        /// <summary>Number of phase-certificate invalidations caused by phase mismatch.</summary>
        public long PhaseCertificatePhaseMismatchInvalidations { get; private set; }

        /// <summary>Most recent phase-certificate invalidation reason.</summary>
        public ReplayPhaseInvalidationReason LastPhaseCertificateInvalidationReason { get; private set; }

        /// <summary>Reference empty/NOP slots observed before replay-stable masking is applied.</summary>
        public long DeterminismReferenceOpportunitySlots { get; private set; }

        /// <summary>Replay-stable empty slots that remained eligible after deterministic masking.</summary>
        public long DeterminismReplayEligibleSlots { get; private set; }

        /// <summary>Empty slots that an unconstrained contour could consider but replay-stable masking rejected.</summary>
        public long DeterminismMaskedSlots { get; private set; }

        /// <summary>Bounded estimate of lost injections caused by deterministic replay constraints.</summary>
        public long DeterminismEstimatedLostSlots { get; private set; }

        /// <summary>Scheduling cycles where deterministic replay constraints removed at least one otherwise empty slot.</summary>
        public long DeterminismConstrainedCycles { get; private set; }

        /// <summary>Domain-isolation probes observed while screening or verifying injected candidates.</summary>
        public long DomainIsolationProbeAttempts { get; private set; }

        /// <summary>Domain-isolation probes blocked by policy.</summary>
        public long DomainIsolationBlockedAttempts { get; private set; }

        /// <summary>Blocked probes caused by disjoint non-kernel domain tags.</summary>
        public long DomainIsolationCrossDomainBlocks { get; private set; }

        /// <summary>Blocked probes caused by kernel-to-user isolation enforcement.</summary>
        public long DomainIsolationKernelToUserBlocks { get; private set; }

        // ── Phase 04: Serialising-Event Epoch State ───────────────────

        /// <summary>
        /// Monotonically increasing epoch counter, bumped once per
        /// <see cref="NotifySerializingCommit"/> call (G34).
        /// HLS: 32-bit up-counter, written only on serialising commit.
        /// </summary>
        private long _serializingEpochCounter;

        /// <summary>
        /// Number of epoch boundaries created by <see cref="NotifySerializingCommit"/>.
        /// Exposed for telemetry; equals the total number of serialising instructions committed.
        /// </summary>
        public long SerializingEpochCount => _serializingEpochCounter;

        /// <summary>
        /// Number of <see cref="PackBundleIntraCoreSmt"/> calls short-circuited because
        /// the owner bundle contained a <see cref="Arch.SerializationClass.FullSerial"/>
        /// or <see cref="Arch.SerializationClass.VmxSerial"/> operation (G33).
        /// </summary>
        public long SerializingBoundaryRejects { get; private set; }

        // ── Phase 02: Class-Capacity Vacancy Model ───────────────────

        /// <summary>
        /// Per-cycle class-capacity snapshot, updated at the start of each
        /// PackBundle / PackBundleIntraCoreSmt cycle.
        /// Used by class-admission pre-checks (Phase 03+).
        /// <para>HLS: 36-flip-flop register, written once per cycle.</para>
        /// </summary>
        private SlotClassCapacityState _classCapacity;

        // ── Phase 03: Two-Stage Admission Telemetry ─────────────────

        /// <summary>Number of class-capacity rejections (StaticClassOvercommit + DynamicClassExhaustion).</summary>
        public long ClassCapacityRejects { get; private set; }

        /// <summary>Number of lane-conflict rejections (PinnedLaneConflict + LateBindingConflict).</summary>
        public long LaneConflictRejects { get; private set; }

        /// <summary>Number of resource-conflict rejections from Stage A (CanInject failed).</summary>
        public long TypedSlotResourceConflictRejects { get; private set; }

        /// <summary>Number of SMT owner-context guard-plane rejects hidden behind ResourceConflict.</summary>
        public long SmtOwnerContextGuardRejects { get; private set; }

        /// <summary>Number of SMT domain guard-plane rejects hidden behind ResourceConflict.</summary>
        public long SmtDomainGuardRejects { get; private set; }

        /// <summary>Number of SMT boundary guard-plane rejects.</summary>
        public long SmtBoundaryGuardRejects { get; private set; }

        /// <summary>Number of SMT shared-resource certificate rejects hidden behind ResourceConflict.</summary>
        public long SmtSharedResourceCertificateRejects { get; private set; }

        /// <summary>Number of SMT register-group certificate rejects hidden behind ResourceConflict.</summary>
        public long SmtRegisterGroupCertificateRejects { get; private set; }

        /// <summary>Most recent SMT legality reject kind observed by typed-slot admission.</summary>
        public RejectKind LastSmtLegalityRejectKind => _lastSmtLegalityRejectKind;

        /// <summary>Most recent SMT legality authority source observed by typed-slot admission.</summary>
        public LegalityAuthoritySource LastSmtLegalityAuthoritySource => _lastSmtLegalityAuthoritySource;

        /// <summary>SMT legality rejections attributed to ALU-class candidates.</summary>
        public long SmtLegalityRejectByAluClass { get; private set; }

        /// <summary>SMT legality rejections attributed to LSU-class candidates.</summary>
        public long SmtLegalityRejectByLsuClass { get; private set; }

        /// <summary>SMT legality rejections attributed to DMA/Stream-class candidates.</summary>
        public long SmtLegalityRejectByDmaStreamClass { get; private set; }

        /// <summary>SMT legality rejections attributed to Branch/Control candidates.</summary>
        public long SmtLegalityRejectByBranchControl { get; private set; }

        /// <summary>SMT legality rejections attributed to SystemSingleton candidates.</summary>
        public long SmtLegalityRejectBySystemSingleton { get; private set; }

        /// <summary>
        /// Number of scoreboard rejections from Stage A
        /// (legacy SuppressLsu or predicted per-VT scoreboard saturation).
        /// </summary>
        public long TypedSlotScoreboardRejects { get; private set; }

        /// <summary>Number of bank-pending rejections from Stage A.</summary>
        public long TypedSlotBankPendingRejects { get; private set; }

        public long BankPendingRejectBank0 { get; private set; }

        public long BankPendingRejectBank1 { get; private set; }

        public long BankPendingRejectBank2 { get; private set; }

        public long BankPendingRejectBank3 { get; private set; }

        public long BankPendingRejectBank4 { get; private set; }

        public long BankPendingRejectBank5 { get; private set; }

        public long BankPendingRejectBank6 { get; private set; }

        public long BankPendingRejectBank7 { get; private set; }

        public long BankPendingRejectBank8 { get; private set; }

        public long BankPendingRejectBank9 { get; private set; }

        public long BankPendingRejectBank10 { get; private set; }

        public long BankPendingRejectBank11 { get; private set; }

        public long BankPendingRejectBank12 { get; private set; }

        public long BankPendingRejectBank13 { get; private set; }

        public long BankPendingRejectBank14 { get; private set; }

        public long BankPendingRejectBank15 { get; private set; }

        public long MemoryClusteringEvents { get; private set; }

        /// <summary>Number of speculation-budget rejections from Stage A.</summary>
        public long TypedSlotSpeculationBudgetRejects { get; private set; }

        /// <summary>Number of sampled hardware-budget rejections for widened LSU load/store injection.</summary>
        public long TypedSlotHardwareBudgetRejects { get; private set; }

        /// <summary>Number of dedicated assist-quota rejections.</summary>
        public long TypedSlotAssistQuotaRejects { get; private set; }

        /// <summary>Number of explicit assist backpressure rejections.</summary>
        public long TypedSlotAssistBackpressureRejects { get; private set; }

        // ── Phase 04: Deterministic Lane Chooser ─────────────────────

        /// <summary>
        /// Per-class previous-lane record for Tier 2 replay lane reuse.
        /// Indexed by SlotClass (cast to byte). Reset on replay phase change.
        /// HLS: 6 × 4-bit register (3-bit lane + 1-bit valid) = 24 flip-flops.
        /// </summary>
        private readonly int[] _previousPhaseLane = new int[8];

        /// <summary>
        /// Epoch ID of the last replay phase that recorded lane assignments.
        /// Used to detect epoch transitions and reset <see cref="_previousPhaseLane"/>.
        /// </summary>
        private ulong _previousPhaseEpochId;

        /// <summary>Number of Tier 2 lane reuse hits (replay phase, previous lane available).</summary>
        public long LaneReuseHits { get; private set; }

        /// <summary>Number of Tier 2 lane reuse misses (fallback to Tier 1).</summary>
        public long LaneReuseMisses { get; private set; }

        // ── Phase 07: Class-Level Replay Templates ───────────────────

        /// <summary>
        /// Cached class-capacity template for replay reuse (Level 1, stable).
        /// Captures per-class free capacity pattern at first successful injection in replay epoch.
        /// HLS: 15 flip-flops (5 × 3-bit values).
        /// </summary>
        private ClassCapacityTemplate _classCapacityTemplate;

        /// <summary>
        /// Whether <see cref="_classCapacityTemplate"/> holds a valid captured template.
        /// HLS: 1 flip-flop.
        /// </summary>
        private bool _classTemplateValid;

        /// <summary>
        /// Domain scope ID at the time the class template was captured.
        /// Template is invalidated when current domain differs.
        /// HLS: 8-bit register.
        /// </summary>
        private int _classTemplateDomainId;

        /// <summary>Number of Level 1 class-template reuse hits (template compatible with current capacity).</summary>
        public long ClassTemplateReuseHits { get; private set; }

        /// <summary>Number of class-template invalidations (domain boundary, capacity mismatch, expired).</summary>
        public long ClassTemplateInvalidations { get; private set; }

        /// <summary>Number of fast-path accepts where template budget confirmed class capacity.</summary>
        public long TypedSlotFastPathAccepts { get; private set; }

        // ── Phase 08: Typed-Slot Telemetry ─────────────────────────

        /// <summary>Disaggregated: compiler bundle exceeds class capacity (sub-count of ClassCapacityRejects).</summary>
        public long StaticClassOvercommitRejects { get; private set; }

        /// <summary>Disaggregated: intra-cycle typed-slot densification exhaustion (sub-count of ClassCapacityRejects).</summary>
        public long DynamicClassExhaustionRejects { get; private set; }

        /// <summary>Disaggregated: hard-pinned lane occupied (sub-count of LaneConflictRejects).</summary>
        public long PinnedLaneConflicts { get; private set; }

        /// <summary>Disaggregated: all class lanes occupied (sub-count of LaneConflictRejects).</summary>
        public long LateBindingConflicts { get; private set; }

        /// <summary>Domain isolation gating reject.</summary>
        public long TypedSlotDomainRejects { get; private set; }

        /// <summary>Admission via full pipeline (complement to TypedSlotFastPathAccepts).</summary>
        public long TypedSlotStandardPathAccepts { get; private set; }

        /// <summary>Total successful late lane bindings.</summary>
        public long TotalLaneBindings { get; private set; }

        /// <summary>Template invalidated by domain boundary (sub-count of ClassTemplateInvalidations).</summary>
        public long ClassTemplateDomainInvalidations { get; private set; }

        /// <summary>Template invalidated by capacity mismatch (sub-count of ClassTemplateInvalidations).</summary>
        public long ClassTemplateCapacityMismatchInvalidations { get; private set; }

        /// <summary>NOPs caused by hard-pinned lane unavailability.</summary>
        public long NopDueToPinnedConstraint { get; private set; }

        /// <summary>NOPs caused by class-capacity exhaustion.</summary>
        public long NopDueToNoClassCapacity { get; private set; }

        /// <summary>NOPs caused by SafetyMask conflict.</summary>
        public long NopDueToResourceConflict { get; private set; }

        /// <summary>NOPs caused by dynamic runtime state.</summary>
        public long NopDueToDynamicState { get; private set; }

        /// <summary>NOPs avoided thanks to class-flexible placement.</summary>
        public long NopAvoided { get; private set; }

        /// <summary>Successful injections into ALU-class lanes.</summary>
        public long AluClassInjects { get; private set; }

        /// <summary>Successful injections into LSU-class lanes.</summary>
        public long LsuClassInjects { get; private set; }

        /// <summary>Successful injections into DMA/Stream-class lanes.</summary>
        public long DmaStreamClassInjects { get; private set; }

        /// <summary>Injections into Branch/Control lanes.</summary>
        public long BranchControlInjects { get; private set; }

        /// <summary>Certificate rejections attributed to ALU-class candidates.</summary>
        public long CertificateRejectByAluClass { get; private set; }

        /// <summary>Certificate rejections attributed to LSU-class candidates.</summary>
        public long CertificateRejectByLsuClass { get; private set; }

        /// <summary>Certificate rejections attributed to DMA/Stream-class candidates.</summary>
        public long CertificateRejectByDmaStreamClass { get; private set; }

        /// <summary>Certificate rejections attributed to Branch/Control candidates.</summary>
        public long CertificateRejectByBranchControl { get; private set; }

        /// <summary>Certificate rejections attributed to SystemSingleton candidates.</summary>
        public long CertificateRejectBySystemSingleton { get; private set; }

        /// <summary>Register-group certificate conflicts observed for VT0.</summary>
        public long CertificateRegGroupConflictVT0 => RegGroupConflictsVT0;

        /// <summary>Register-group certificate conflicts observed for VT1.</summary>
        public long CertificateRegGroupConflictVT1 => RegGroupConflictsVT1;

        /// <summary>Register-group certificate conflicts observed for VT2.</summary>
        public long CertificateRegGroupConflictVT2 => RegGroupConflictsVT2;

        /// <summary>Register-group certificate conflicts observed for VT3.</summary>
        public long CertificateRegGroupConflictVT3 => RegGroupConflictsVT3;

        /// <summary>Total scheduler rejections observed for VT0.</summary>
        public long RejectionsVT0 { get; private set; }

        /// <summary>Total scheduler rejections observed for VT1.</summary>
        public long RejectionsVT1 { get; private set; }

        /// <summary>Total scheduler rejections observed for VT2.</summary>
        public long RejectionsVT2 { get; private set; }

        /// <summary>Total scheduler rejections observed for VT3.</summary>
        public long RejectionsVT3 { get; private set; }

        /// <summary>Register-group certificate conflicts observed for VT0.</summary>
        public long RegGroupConflictsVT0 { get; private set; }

        /// <summary>Register-group certificate conflicts observed for VT1.</summary>
        public long RegGroupConflictsVT1 { get; private set; }

        /// <summary>Register-group certificate conflicts observed for VT2.</summary>
        public long RegGroupConflictsVT2 { get; private set; }

        /// <summary>Register-group certificate conflicts observed for VT3.</summary>
        public long RegGroupConflictsVT3 { get; private set; }

        /// <summary>Injections of hard-pinned ops.</summary>
        public long HardPinnedInjects { get; private set; }

        /// <summary>Injections of class-flexible ops.</summary>
        public long ClassFlexibleInjects { get; private set; }

        /// <summary>
        /// Get the previous lane used for a class in the current replay phase.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPreviousPhaseLane(SlotClass slotClass)
        {
            byte idx = (byte)slotClass;
            return idx < 8 ? _previousPhaseLane[idx] : -1;
        }

        /// <summary>
        /// Record the lane selected for a class in the current replay phase.
        /// Called after successful TryMaterializeLane for ClassFlexible ops.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordPhaseLane(SlotClass slotClass, int lane)
        {
            byte idx = (byte)slotClass;
            if (idx < 8) _previousPhaseLane[idx] = lane;
        }

        /// <summary>
        /// Reset all per-class previous-lane records to invalid (-1).
        /// Called on replay phase deactivation or epoch transition.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetPreviousPhaseLanes()
        {
            for (int i = 0; i < _previousPhaseLane.Length; i++)
                _previousPhaseLane[i] = -1;
        }

        // ── Phase 07: Class Template Capture / Match / Invalidation ──

        /// <summary>
        /// Phase 07: Capture a class-capacity template from current capacity state.
        /// Called on first successful typed-slot injection in an active replay epoch.
        /// Gated behind <see cref="TypedSlotEnabled"/>.
        /// <para>HLS: 5 × 3-bit latch writes — single cycle.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CaptureClassTemplate(int domainScopeId)
        {
            _classCapacityTemplate = new ClassCapacityTemplate(_classCapacity);
            _classTemplateValid = true;
            _classTemplateDomainId = domainScopeId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveClassTemplateDomainScopeId(
            SmtBundleMetadata4Way bundleMetadata)
        {
            int ownerContextId = bundleMetadata.HasKnownOwnerContext
                ? bundleMetadata.OwnerContextId
                : bundleMetadata.OwnerVirtualThreadId;

            if (ownerContextId == bundleMetadata.OwnerVirtualThreadId &&
                bundleMetadata.OwnerDomainTag == 0)
            {
                return ownerContextId;
            }

            var domainScopeHasher = new HardwareHash();
            domainScopeHasher.Initialize();
            domainScopeHasher.Compress((ulong)(uint)bundleMetadata.OwnerVirtualThreadId);
            domainScopeHasher.Compress((ulong)(uint)ownerContextId);
            domainScopeHasher.Compress(bundleMetadata.OwnerDomainTag);
            return unchecked((int)domainScopeHasher.Finalize());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordLoopPhaseEntry(ReplayPhaseContext phase)
        {
            if (!EnableLoopPhaseSampling || !phase.IsActive)
                return;

            if (phase.EpochId == _lastLoopPhaseEntryEpochId && phase.CachedPc == _lastLoopPhaseEntryPc)
                return;

            _lastLoopPhaseEntryEpochId = phase.EpochId;
            _lastLoopPhaseEntryPc = phase.CachedPc;

            int phaseEntryCount = _loopPhaseEntryCounts.TryGetValue(phase.CachedPc, out int currentCount)
                ? currentCount + 1
                : 1;

            _loopPhaseEntryCounts[phase.CachedPc] = phaseEntryCount;
            PruneLoopPhaseHeat();

            if (_loopPhaseTrackers.TryGetValue(phase.CachedPc, out LoopPhaseTracker? existingTracker))
            {
                existingTracker.SetPhaseEntryCount(phaseEntryCount);
                return;
            }

            if (phaseEntryCount < LoopPhaseActivationThreshold)
                return;

            PromoteLoopPhaseTracker(phase.CachedPc, phaseEntryCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordLoopPhaseSample()
        {
            if (!EnableLoopPhaseSampling || !_currentReplayPhase.IsActive)
                return;

            if (_currentReplayPhase.EpochId == _lastLoopPhaseSampleEpochId &&
                _currentReplayPhase.CachedPc == _lastLoopPhaseSamplePc)
            {
                return;
            }

            ulong loopPcAddress = _currentReplayPhase.CachedPc;
            if (!_loopPhaseEntryCounts.TryGetValue(loopPcAddress, out int phaseEntryCount) ||
                phaseEntryCount < LoopPhaseActivationThreshold)
            {
                return;
            }

            if (!_loopPhaseTrackers.TryGetValue(loopPcAddress, out LoopPhaseTracker? tracker))
            {
                PromoteLoopPhaseTracker(loopPcAddress, phaseEntryCount);
                if (!_loopPhaseTrackers.TryGetValue(loopPcAddress, out tracker))
                {
                    return;
                }
            }

            _lastLoopPhaseSampleEpochId = _currentReplayPhase.EpochId;
            _lastLoopPhaseSamplePc = loopPcAddress;
            tracker.SetPhaseEntryCount(phaseEntryCount);
            tracker.AddSample(new ClassCapacityTemplate(_classCapacity));
        }

        private void PromoteLoopPhaseTracker(ulong loopPcAddress, int phaseEntryCount)
        {
            if (_loopPhaseTrackers.TryGetValue(loopPcAddress, out LoopPhaseTracker? tracker))
            {
                tracker.SetPhaseEntryCount(phaseEntryCount);
                return;
            }

            if (_loopPhaseTrackers.Count < LoopPhaseExportLimit)
            {
                _loopPhaseTrackers[loopPcAddress] = new LoopPhaseTracker(loopPcAddress, phaseEntryCount);
                return;
            }

            ulong coldestLoopPc = 0;
            int coldestHeat = int.MaxValue;
            foreach (KeyValuePair<ulong, LoopPhaseTracker> entry in _loopPhaseTrackers)
            {
                int candidateHeat = entry.Value.PhaseEntryCount;
                if (candidateHeat < coldestHeat ||
                    (candidateHeat == coldestHeat && entry.Key > coldestLoopPc))
                {
                    coldestLoopPc = entry.Key;
                    coldestHeat = candidateHeat;
                }
            }

            if (phaseEntryCount <= coldestHeat)
                return;

            _loopPhaseTrackers.Remove(coldestLoopPc);
            _loopPhaseTrackers[loopPcAddress] = new LoopPhaseTracker(loopPcAddress, phaseEntryCount);
        }

        private void PruneLoopPhaseHeat()
        {
            if (_loopPhaseEntryCounts.Count <= LoopPhaseHeatPruneThreshold)
                return;

            var entries = new List<KeyValuePair<ulong, int>>(_loopPhaseEntryCounts);
            entries.Sort(static (left, right) =>
            {
                int heatCompare = right.Value.CompareTo(left.Value);
                return heatCompare != 0 ? heatCompare : left.Key.CompareTo(right.Key);
            });

            _loopPhaseEntryCounts.Clear();

            int keepCount = Math.Min(LoopPhaseExportLimit * 2, entries.Count);
            for (int index = 0; index < keepCount; index++)
            {
                KeyValuePair<ulong, int> entry = entries[index];
                _loopPhaseEntryCounts[entry.Key] = entry.Value;
            }

            foreach (KeyValuePair<ulong, LoopPhaseTracker> entry in _loopPhaseTrackers)
            {
                if (!_loopPhaseEntryCounts.ContainsKey(entry.Key))
                {
                    _loopPhaseEntryCounts[entry.Key] = entry.Value.PhaseEntryCount;
                }
            }
        }

        private IReadOnlyList<LoopPhaseClassProfile>? BuildLoopPhaseProfiles()
        {
            if (!EnableLoopPhaseSampling || _loopPhaseTrackers.Count == 0)
                return null;

            var exportCandidates = new List<(LoopPhaseTracker Tracker, LoopPhaseClassProfile Profile)>(_loopPhaseTrackers.Count);
            foreach (LoopPhaseTracker tracker in _loopPhaseTrackers.Values)
            {
                if (tracker.IterationsSampled <= 0)
                    continue;

                exportCandidates.Add((tracker, tracker.BuildProfile()));
            }

            if (exportCandidates.Count == 0)
                return null;

            exportCandidates.Sort(static (left, right) =>
            {
                int heatCompare = right.Tracker.PhaseEntryCount.CompareTo(left.Tracker.PhaseEntryCount);
                if (heatCompare != 0)
                    return heatCompare;

                int varianceCompare = right.Profile.OverallClassVariance.CompareTo(left.Profile.OverallClassVariance);
                if (varianceCompare != 0)
                    return varianceCompare;

                return left.Profile.LoopPcAddress.CompareTo(right.Profile.LoopPcAddress);
            });

            int exportCount = Math.Min(LoopPhaseExportLimit, exportCandidates.Count);
            var profiles = new List<LoopPhaseClassProfile>(exportCount);
            for (int index = 0; index < exportCount; index++)
            {
                profiles.Add(exportCandidates[index].Profile);
            }

            return profiles;
        }

        /// <summary>
        /// Phase 07: Check if class template matches current state and build
        /// explicit per-cycle template-admission state for the decrement-aware fast path.
        /// <para>HLS: 5 × 3-bit comparators + 1 comparator (domain) = ~6 LUTs.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ClassTemplateAdmissionState PrepareClassTemplateAdmissionState(int currentDomainId)
        {
            if (!_classTemplateValid || !_currentReplayPhase.IsActive)
                return default;

            if (_classTemplateDomainId != currentDomainId)
            {
                InvalidateClassTemplate(ReplayPhaseInvalidationReason.DomainBoundary);
                return default;
            }

            if (!_classCapacityTemplate.IsCompatibleWith(_classCapacity))
            {
                InvalidateClassTemplate(ReplayPhaseInvalidationReason.ClassCapacityMismatch);
                return default;
            }

            ClassTemplateReuseHits++;
            return ClassTemplateAdmissionState.Create(new TemplateBudget(_classCapacityTemplate));
        }

        /// <summary>
        /// Phase 07: Invalidate the class-capacity template.
        /// <para>HLS: 1-bit clear + diagnostic counter increment.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvalidateClassTemplate(ReplayPhaseInvalidationReason reason)
        {
            if (_classTemplateValid)
            {
                _classTemplateValid = false;
                ClassTemplateInvalidations++;
                LastPhaseCertificateInvalidationReason = reason;

                // Phase 08: disaggregated invalidation tracking
                switch (reason)
                {
                    case ReplayPhaseInvalidationReason.DomainBoundary:
                        ClassTemplateDomainInvalidations++;
                        break;
                    case ReplayPhaseInvalidationReason.ClassCapacityMismatch:
                        ClassTemplateCapacityMismatchInvalidations++;
                        break;
                }
            }
        }

        // ── Constructor ──────────────────────────────────────────────

        public MicroOpScheduler(
            IRuntimeLegalityService? runtimeLegalityService = null)
        {
            _runtimeLegalityService =
                runtimeLegalityService ??
                RuntimeLegalityServiceFactory.CreateDefault(this);

            // Initialize all scoreboard slots to -1 (free)
            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
                _scoreboard[i] = -1;

            for (int vt = 0; vt < SMT_WAYS; vt++)
                for (int s = 0; s < SCOREBOARD_SLOTS; s++)
                {
                    _smtScoreboard[vt, s] = -1;
                    _smtScoreboardType[vt, s] = ScoreboardEntryType.Free;
                    _smtScoreboardBankId[vt, s] = -1;
                }

            // Phase 2C: Initialize speculation budget to max
            _speculationBudget = SCOREBOARD_SLOTS / 2;

            // Phase 02: Initialize class-capacity totals from lane map
            _classCapacity.InitializeFromLaneMap();

            // Phase 04: Initialize per-class previous-lane to invalid (-1)
            for (int i = 0; i < _previousPhaseLane.Length; i++)
                _previousPhaseLane[i] = -1;

            // Phase 07: Initialize class template state
            _classTemplateValid = false;
            _classTemplateDomainId = 0;
        }

        /// <summary>
        /// Quick pre-check: is there class capacity for a candidate operation?
        /// Called before full CanInject() to avoid expensive mask checks
        /// when the class is already full.
        /// <para>HLS: single 3-bit comparator (1 LUT layer).</para>
        /// </summary>
        /// <param name="candidate">The micro-operation to check capacity for.</param>
        /// <returns><see langword="true"/> if the candidate's required slot class has free capacity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasClassCapacityFor(MicroOp candidate)
        {
            return _classCapacity.HasFreeCapacity(candidate.Placement.RequiredSlotClass);
        }

        /// <summary>
        /// Gets the current class-capacity snapshot (read-only copy for diagnostics/testing).
        /// </summary>
        public SlotClassCapacityState GetClassCapacitySnapshot() => _classCapacity;

        /// <summary>
        /// Returns the total number of successful injections attributed to one virtual thread.
        /// This is a production diagnostics surface consumed by telemetry export.
        /// </summary>
        public long GetPerVtInjectionCount(int vtId)
        {
            if ((uint)vtId >= SMT_WAYS)
                return 0;

            return _perVtInjections[vtId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InterCoreNominationSnapshot CaptureInterCoreNominationSnapshot()
        {
#if TESTING
            if (TestMode && TestNominationQueues is not null)
            {
                return InterCoreNominationSnapshot.Capture(TestNominationQueues);
            }
#endif

            return InterCoreNominationSnapshot.Capture(_ports, _portValid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConsumeInterCoreNomination(int coreId)
        {
            if ((uint)coreId >= NUM_PORTS)
                return;

#if TESTING
            if (TestMode && TestNominationQueues is not null && coreId < TestNominationQueues.Length)
            {
                if (TestNominationQueues[coreId].Count > 0)
                {
                    TestNominationQueues[coreId].Dequeue();
                }

                return;
            }
#endif

            _ports[coreId] = null;
            _portValid[coreId] = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SmtNominationSnapshot CaptureSmtNominationSnapshot(SmtNominationState nominationState)
        {
            return SmtNominationSnapshot.Capture(_smtPorts, nominationState);
        }
    }
    /// <summary>
    /// Exception thrown when a critical pipeline invariant is violated.
    /// Indicates an unrecoverable hardware fault in the emulator model.
    /// </summary>
    public class PipelinePanicException : Exception
    {
        public PipelinePanicException(string message) : base(message)
        {
        }
    }
}
