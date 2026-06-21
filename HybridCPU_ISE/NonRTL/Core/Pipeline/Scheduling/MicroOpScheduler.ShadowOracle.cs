using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Reason category for oracle-gap decomposition.
    /// Each bucket explains why the real scheduler missed an oracle-legal slot.
    /// </summary>
    public enum OracleGapCategory : byte
    {
        /// <summary>No gap — real scheduler matched oracle packing.</summary>
        None = 0,

        /// <summary>Gap caused by replay-stable donor mask restricting injectable slots.</summary>
        DonorRestriction,

        /// <summary>Gap caused by fairness/credit or TDM ordering skipping a ready candidate.</summary>
        FairnessOrdering,

        /// <summary>Gap caused by legality-layer conservatism (safety mask false reject).</summary>
        LegalityConservatism,

        /// <summary>Gap caused by domain-isolation or kernel-to-user enforcement.</summary>
        DomainIsolation,

        /// <summary>Gap caused by speculation budget exhaustion.</summary>
        SpeculationBudget,

        /// <summary>Phase 07: Gap caused by class-capacity template divergence between runs.</summary>
        ClassCapacityDivergence
    }

    /// <summary>
    /// Per-cycle oracle gap record: captures the difference between oracle and real packing.
    /// </summary>
    public readonly struct OracleGapRecord
    {
        public OracleGapRecord(
            long cycle,
            int oraclePackedSlots,
            int realPackedSlots,
            OracleGapCategory primaryCategory)
        {
            Cycle = cycle;
            OraclePackedSlots = oraclePackedSlots;
            RealPackedSlots = realPackedSlots;
            PrimaryCategory = primaryCategory;
        }

        /// <summary>Scheduling cycle when this gap was observed.</summary>
        public long Cycle { get; }

        /// <summary>Number of slots the oracle reference contour packed.</summary>
        public int OraclePackedSlots { get; }

        /// <summary>Number of slots the real scheduler packed.</summary>
        public int RealPackedSlots { get; }

        /// <summary>Primary reason bucket for the gap.</summary>
        public OracleGapCategory PrimaryCategory { get; }

        /// <summary>Gap magnitude (oracle - real). Zero or positive.</summary>
        public int Gap => Math.Max(0, OraclePackedSlots - RealPackedSlots);
    }

    /// <summary>
    /// Structured counterexample: a specific scheduling scenario where the real scheduler
    /// missed an oracle-legal injection and the reason can be localized.
    /// </summary>
    public readonly struct OracleCounterexample
    {
        public OracleCounterexample(
            long cycle,
            OracleGapCategory category,
            int missedSlots,
            string description)
        {
            Cycle = cycle;
            Category = category;
            MissedSlots = missedSlots;
            Description = description;
        }

        /// <summary>Cycle where the counterexample was observed.</summary>
        public long Cycle { get; }

        /// <summary>Reason bucket classifying the false reject or missed opportunity.</summary>
        public OracleGapCategory Category { get; }

        /// <summary>Number of slots missed in this counterexample.</summary>
        public int MissedSlots { get; }

        /// <summary>Human-readable description of the divergence shape.</summary>
        public string Description { get; }

        public string Describe() =>
            $"Cycle {Cycle}: {Category}, {MissedSlots} missed slot(s) — {Description}";
    }

    /// <summary>
    /// Aggregated oracle-gap summary across all scheduling cycles.
    /// </summary>
    public readonly struct OracleGapSummary
    {
        public OracleGapSummary(
            long totalOracleSlots,
            long totalRealSlots,
            long donorRestrictionGap,
            long fairnessOrderingGap,
            long legalityConservatismGap,
            long domainIsolationGap,
            long speculationBudgetGap,
            long cyclesWithGap,
            long totalCyclesAnalyzed)
        {
            TotalOracleSlots = totalOracleSlots;
            TotalRealSlots = totalRealSlots;
            DonorRestrictionGap = donorRestrictionGap;
            FairnessOrderingGap = fairnessOrderingGap;
            LegalityConservatismGap = legalityConservatismGap;
            DomainIsolationGap = domainIsolationGap;
            SpeculationBudgetGap = speculationBudgetGap;
            CyclesWithGap = cyclesWithGap;
            TotalCyclesAnalyzed = totalCyclesAnalyzed;
        }

        /// <summary>Total slots packed by oracle across all analyzed cycles.</summary>
        public long TotalOracleSlots { get; }

        /// <summary>Total slots packed by real scheduler across all analyzed cycles.</summary>
        public long TotalRealSlots { get; }

        /// <summary>Gap slots attributed to donor/replay mask restrictions.</summary>
        public long DonorRestrictionGap { get; }

        /// <summary>Gap slots attributed to fairness/TDM ordering.</summary>
        public long FairnessOrderingGap { get; }

        /// <summary>Gap slots attributed to legality-layer conservatism.</summary>
        public long LegalityConservatismGap { get; }

        /// <summary>Gap slots attributed to domain-isolation enforcement.</summary>
        public long DomainIsolationGap { get; }

        /// <summary>Gap slots attributed to speculation budget exhaustion.</summary>
        public long SpeculationBudgetGap { get; }

        /// <summary>Number of scheduling cycles where oracle packed more than real.</summary>
        public long CyclesWithGap { get; }

        /// <summary>Total scheduling cycles analyzed.</summary>
        public long TotalCyclesAnalyzed { get; }

        /// <summary>Total gap (oracle - real) across all categories.</summary>
        public long TotalGap => TotalOracleSlots - TotalRealSlots;

        /// <summary>Oracle efficiency rate: what fraction of oracle slots the real scheduler captured.</summary>
        public double OracleEfficiency =>
            TotalOracleSlots == 0 ? 1.0 : (double)TotalRealSlots / TotalOracleSlots;

        /// <summary>Gap rate: fraction of oracle slots missed by the real scheduler.</summary>
        public double GapRate =>
            TotalOracleSlots == 0 ? 0.0 : (double)TotalGap / TotalOracleSlots;

        public string Describe()
        {
            if (TotalGap <= 0)
                return $"No oracle gap detected across {TotalCyclesAnalyzed} cycles.";

            return $"Oracle gap: {TotalGap} slots ({GapRate:P2}) across {CyclesWithGap}/{TotalCyclesAnalyzed} cycles. " +
                   $"Donor restriction: {DonorRestrictionGap}, fairness ordering: {FairnessOrderingGap}, " +
                   $"legality conservatism: {LegalityConservatismGap}, domain isolation: {DomainIsolationGap}, " +
                   $"speculation budget: {SpeculationBudgetGap}.";
        }
    }

    /// <summary>
    /// Phase 5: Shadow Oracle Scheduler — legality-preserving reference contour.
    ///
    /// This partial class adds a diagnostic shadow-oracle path that estimates
    /// what a stronger scheduler could have packed under the same bundle and
    /// safety contract, but without replay-stable donor mask restrictions,
    /// fairness ordering, or speculation budget limits.
    ///
    /// The oracle contour is opt-in, comparison-only, and never drives
    /// production scheduling decisions.
    /// </summary>
    public partial class MicroOpScheduler
    {
        // ── Phase 5: Shadow Oracle State ─────────────────────────────

        /// <summary>
        /// Enable shadow oracle reference contour for Phase 5 diagnostics.
        /// When true, each PackBundleIntraCoreSmt call also computes a reference
        /// packing result for gap analysis.
        /// </summary>
        public bool EnableShadowOracle { get; set; }

        private long _oracleTotalSlots;
        private long _oracleRealSlots;
        private long _oracleDonorRestrictionGap;
        private long _oracleFairnessOrderingGap;
        private long _oracleLegalityConservatismGap;
        private long _oracleDomainIsolationGap;
        private long _oracleSpeculationBudgetGap;
        private long _oracleCyclesWithGap;
        private long _oracleTotalCyclesAnalyzed;

        /// <summary>Maximum number of counterexamples retained (bounded buffer).</summary>
        private const int MAX_COUNTEREXAMPLES = 64;

        private readonly List<OracleCounterexample> _counterexamples = new(MAX_COUNTEREXAMPLES);

        private readonly struct ShadowOracleCycleSnapshot
        {
            private readonly MicroOp?[]? _bundleSlots;

            public ShadowOracleCycleSnapshot(
                MicroOp?[] bundleSlots,
                SmtNominationSnapshot nominationSnapshot)
            {
                _bundleSlots = bundleSlots;
                NominationSnapshot = nominationSnapshot;
            }

            public SmtNominationSnapshot NominationSnapshot { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetBundleSlot(int slotIndex, out MicroOp? bundleOp)
            {
                bundleOp = null;
                MicroOp?[]? bundleSlots = _bundleSlots;
                if (bundleSlots is null || (uint)slotIndex >= 8)
                    return false;

                bundleOp = bundleSlots[slotIndex];
                return true;
            }

            public static ShadowOracleCycleSnapshot Capture(
                MicroOp[] bundle,
                SmtNominationSnapshot nominationSnapshot)
            {
                var bundleSlots = new MicroOp?[8];
                Array.Copy(bundle, bundleSlots, 8);
                return new ShadowOracleCycleSnapshot(bundleSlots, nominationSnapshot);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ShadowOracleCycleSnapshot CaptureShadowOracleCycleSnapshot(
            MicroOp[] bundle,
            SmtNominationState nominationState)
        {
            return ShadowOracleCycleSnapshot.Capture(
                bundle,
                CaptureSmtNominationSnapshot(nominationState));
        }

        /// <summary>
        /// Get the aggregated oracle-gap summary for Phase 5 evidence rendering.
        /// </summary>
        public OracleGapSummary GetOracleGapSummary()
        {
            return new OracleGapSummary(
                _oracleTotalSlots,
                _oracleRealSlots,
                _oracleDonorRestrictionGap,
                _oracleFairnessOrderingGap,
                _oracleLegalityConservatismGap,
                _oracleDomainIsolationGap,
                _oracleSpeculationBudgetGap,
                _oracleCyclesWithGap,
                _oracleTotalCyclesAnalyzed);
        }

        /// <summary>
        /// Get bounded counterexample records for Phase 5 refinement guidance.
        /// </summary>
        public IReadOnlyList<OracleCounterexample> GetCounterexamples() => _counterexamples;

        /// <summary>
        /// Compute the shadow oracle reference packing for the current bundle.
        ///
        /// The oracle ignores:
        /// - Replay-stable donor mask (considers ALL empty slots)
        /// - Fairness credit ordering (tries all candidates)
        /// - Speculation budget limits
        ///
        /// The oracle preserves:
        /// - Bundle size (8 slots)
        /// - Legality and safety masks (BundleResourceCertificate4Way)
        /// - Domain isolation enforcement
        /// - Structural resource limits
        ///
        /// Returns the number of candidates the oracle could have packed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ComputeShadowOraclePackCount(
            ShadowOracleCycleSnapshot cycleSnapshot,
            int ownerVirtualThreadId)
        {
            // Build a fresh certificate for the existing bundle contents
            var oracleCert = BundleResourceCertificate4Way.Empty;
            for (int i = 0; i < 8; i++)
            {
                if (cycleSnapshot.TryGetBundleSlot(i, out MicroOp? bundleOp) && bundleOp is not null)
                {
                    oracleCert.AddOperation(bundleOp);
                }
            }

            // Find all empty slots (ignoring replay-stable donor mask)
            Span<int> emptySlots = stackalloc int[8];
            int emptyCount = 0;
            for (int i = 0; i < 8; i++)
            {
                if (!cycleSnapshot.TryGetBundleSlot(i, out MicroOp? bundleOp) || bundleOp is null)
                {
                    emptySlots[emptyCount++] = i;
                }
            }

            if (emptyCount == 0)
                return 0;

            ushort localOutstandingStoreBankMask = 0;
            byte remainingHardwareMemoryIssueBudget = _hardwareOccupancySnapshot.MemoryIssueBudget;
            uint consumedHardwareMemoryBudgetByBank = 0;
            byte remainingHardwareLoadIssueBudget = _hardwareOccupancySnapshot.ReadIssueBudget;
            byte remainingHardwareStoreIssueBudget = _hardwareOccupancySnapshot.WriteIssueBudget;
            uint consumedHardwareLoadBudgetByBank = 0;
            uint consumedHardwareStoreBudgetByBank = 0;
            byte[] projectedOutstandingMemoryCount = new byte[SMT_WAYS];
            byte[] projectedOutstandingMemoryCapacity = new byte[SMT_WAYS];
            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                int liveOutstandingMemoryCount = GetOutstandingMemoryCount(vt);
                int liveFreeScoreboardSlots = GetFreeSmtScoreboardSlotCount(vt);
                projectedOutstandingMemoryCount[vt] =
                    SaturateScoreboardCount(liveOutstandingMemoryCount);
                projectedOutstandingMemoryCapacity[vt] =
                    SaturateScoreboardCount(liveOutstandingMemoryCount + liveFreeScoreboardSlots);
            }

            for (int i = 0; i < 8; i++)
            {
                if (!cycleSnapshot.TryGetBundleSlot(i, out MicroOp? bundleOp) || bundleOp is null)
                {
                    continue;
                }

                if (bundleOp is LoadStoreMicroOp bundleMemoryOp)
                {
                    IncrementProjectedOutstandingMemoryCount(
                        projectedOutstandingMemoryCount,
                        bundleMemoryOp.VirtualThreadId);

                    if (remainingHardwareMemoryIssueBudget > 0)
                    {
                        remainingHardwareMemoryIssueBudget--;
                    }

                    consumedHardwareMemoryBudgetByBank =
                        IncrementPackedConsumedHardwareBudget(
                            consumedHardwareMemoryBudgetByBank,
                            bundleMemoryOp.MemoryBankId);

                    if (bundleMemoryOp is LoadMicroOp)
                    {
                        if (remainingHardwareLoadIssueBudget > 0)
                        {
                            remainingHardwareLoadIssueBudget--;
                        }

                        consumedHardwareLoadBudgetByBank =
                            IncrementPackedConsumedHardwareBudget(
                                consumedHardwareLoadBudgetByBank,
                                bundleMemoryOp.MemoryBankId);
                    }
                    else if (bundleMemoryOp is StoreMicroOp)
                    {
                        if (remainingHardwareStoreIssueBudget > 0)
                        {
                            remainingHardwareStoreIssueBudget--;
                        }

                        consumedHardwareStoreBudgetByBank =
                            IncrementPackedConsumedHardwareBudget(
                                consumedHardwareStoreBudgetByBank,
                                bundleMemoryOp.MemoryBankId);
                    }
                }

                if (bundleOp is StoreMicroOp bundleStore &&
                    (uint)bundleStore.MemoryBankId < 16)
                {
                    localOutstandingStoreBankMask |= (ushort)(1 << bundleStore.MemoryBankId);
                }
            }

            // Try all SMT candidates in ascending VT order (ignoring fairness ranking).
            // Oracle still ignores speculation budget, but now mirrors the live
            // bank-pending/store-bank plus projected per-VT scoreboard contour
            // and now also mirrors the sampled mixed hardware-memory budget.
            // for the working bundle.
            int oraclePacked = 0;
            int nextEmpty = 0;

            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                if (vt == ownerVirtualThreadId) continue;
                if (!cycleSnapshot.NominationSnapshot.TryGetCandidate(vt, out MicroOp candidate))
                    continue;

                // Oracle preserves legality: check via certificate
                if (!oracleCert.CanInject(candidate))
                    continue;

                // Oracle preserves domain isolation (but NOT speculation budget)
                if (SuppressLsu && candidate.IsMemoryOp)
                    continue;

                int candidateVirtualThreadId = candidate.VirtualThreadId;
                int candidateBankId = candidate is LoadStoreMicroOp lsCandidate ? lsCandidate.MemoryBankId : -1;
                if (candidate.IsMemoryOp && (uint)candidateVirtualThreadId >= SMT_WAYS)
                    continue;
                if (candidate.IsMemoryOp &&
                    projectedOutstandingMemoryCount[candidateVirtualThreadId] >=
                    projectedOutstandingMemoryCapacity[candidateVirtualThreadId])
                    continue;
                if (candidate.IsMemoryOp && candidateBankId >= 0 &&
                    IsBankPendingForVT(candidateBankId, candidateVirtualThreadId))
                    continue;
                if (candidate is StoreMicroOp && candidateBankId >= 0 &&
                    (localOutstandingStoreBankMask & (1 << candidateBankId)) != 0)
                    continue;
                if (candidate is LoadStoreMicroOp &&
                    (!HasOracleHardwareMemoryBudget(
                        candidate,
                        remainingHardwareMemoryIssueBudget,
                        consumedHardwareMemoryBudgetByBank,
                        remainingHardwareLoadIssueBudget,
                        consumedHardwareLoadBudgetByBank,
                        remainingHardwareStoreIssueBudget,
                        consumedHardwareStoreBudgetByBank,
                        candidateBankId)))
                    continue;

                // Find next empty slot (no donor mask filtering)
                if (nextEmpty >= emptyCount)
                    break;

                oracleCert.AddOperation(candidate);
                if (candidate is LoadStoreMicroOp memoryCandidate)
                {
                    IncrementProjectedOutstandingMemoryCount(
                        projectedOutstandingMemoryCount,
                        memoryCandidate.VirtualThreadId);

                    if (remainingHardwareMemoryIssueBudget > 0)
                    {
                        remainingHardwareMemoryIssueBudget--;
                    }

                    consumedHardwareMemoryBudgetByBank =
                        IncrementPackedConsumedHardwareBudget(
                            consumedHardwareMemoryBudgetByBank,
                            memoryCandidate.MemoryBankId);

                    if (memoryCandidate is LoadMicroOp)
                    {
                        if (remainingHardwareLoadIssueBudget > 0)
                        {
                            remainingHardwareLoadIssueBudget--;
                        }

                        consumedHardwareLoadBudgetByBank =
                            IncrementPackedConsumedHardwareBudget(
                                consumedHardwareLoadBudgetByBank,
                                memoryCandidate.MemoryBankId);
                    }
                    else if (memoryCandidate is StoreMicroOp)
                    {
                        if (remainingHardwareStoreIssueBudget > 0)
                        {
                            remainingHardwareStoreIssueBudget--;
                        }

                        consumedHardwareStoreBudgetByBank =
                            IncrementPackedConsumedHardwareBudget(
                                consumedHardwareStoreBudgetByBank,
                                memoryCandidate.MemoryBankId);
                    }
                }
                if (candidate is StoreMicroOp && candidateBankId >= 0 && candidateBankId < 16)
                    localOutstandingStoreBankMask |= (ushort)(1 << candidateBankId);
                oraclePacked++;
                nextEmpty++;
            }

            return oraclePacked;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasOracleHardwareMemoryBudget(
            MicroOp candidate,
            byte remainingHardwareMemoryIssueBudget,
            uint consumedHardwareMemoryBudgetByBank,
            byte remainingHardwareLoadIssueBudget,
            uint consumedHardwareLoadBudgetByBank,
            byte remainingHardwareStoreIssueBudget,
            uint consumedHardwareStoreBudgetByBank,
            int memoryBankId)
        {
            if (remainingHardwareMemoryIssueBudget == 0)
            {
                return false;
            }

            if ((uint)memoryBankId >= 16)
            {
                return true;
            }

            if (GetPackedConsumedHardwareBudget(consumedHardwareMemoryBudgetByBank, memoryBankId) >=
                _hardwareOccupancySnapshot.GetMemoryBudgetForBank(memoryBankId))
            {
                return false;
            }

            if (candidate is LoadMicroOp)
            {
                return HasOracleDirectionalHardwareBudget(
                    remainingHardwareLoadIssueBudget,
                    consumedHardwareLoadBudgetByBank,
                    _hardwareOccupancySnapshot.GetReadBudgetForBank(memoryBankId),
                    memoryBankId);
            }

            if (candidate is StoreMicroOp)
            {
                return HasOracleDirectionalHardwareBudget(
                    remainingHardwareStoreIssueBudget,
                    consumedHardwareStoreBudgetByBank,
                    _hardwareOccupancySnapshot.GetWriteBudgetForBank(memoryBankId),
                    memoryBankId);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasOracleDirectionalHardwareBudget(
            byte remainingDirectionalIssueBudget,
            uint consumedDirectionalBudgetByBank,
            byte bankBudget,
            int memoryBankId)
        {
            if (remainingDirectionalIssueBudget == 0)
            {
                return false;
            }

            if ((uint)memoryBankId >= 16)
            {
                return true;
            }

            return GetPackedConsumedHardwareBudget(consumedDirectionalBudgetByBank, memoryBankId) <
                   bankBudget;
        }

        /// <summary>
        /// Record the oracle gap for a single scheduling cycle and emit counterexamples.
        /// Called at the end of PackBundleIntraCoreSmt when shadow oracle is enabled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordOracleGap(
            ShadowOracleCycleSnapshot cycleSnapshot,
            int ownerVirtualThreadId,
            int realInjectedCount)
        {
            if (!EnableShadowOracle)
                return;

            int oraclePacked = ComputeShadowOraclePackCount(cycleSnapshot, ownerVirtualThreadId);

            _oracleTotalSlots += oraclePacked;
            _oracleRealSlots += realInjectedCount;
            _oracleTotalCyclesAnalyzed++;

            int gap = Math.Max(0, oraclePacked - realInjectedCount);
            if (gap <= 0)
                return;

            _oracleCyclesWithGap++;

            // Classify the primary gap reason
            OracleGapCategory category = ClassifyGapCategory(cycleSnapshot, ownerVirtualThreadId);

            switch (category)
            {
                case OracleGapCategory.DonorRestriction:
                    _oracleDonorRestrictionGap += gap;
                    break;
                case OracleGapCategory.FairnessOrdering:
                    _oracleFairnessOrderingGap += gap;
                    break;
                case OracleGapCategory.LegalityConservatism:
                    _oracleLegalityConservatismGap += gap;
                    break;
                case OracleGapCategory.DomainIsolation:
                    _oracleDomainIsolationGap += gap;
                    break;
                case OracleGapCategory.SpeculationBudget:
                    _oracleSpeculationBudgetGap += gap;
                    break;
            }

            // Emit bounded counterexample
            if (_counterexamples.Count < MAX_COUNTEREXAMPLES)
            {
                string description = category switch
                {
                    OracleGapCategory.DonorRestriction =>
                        $"Replay-stable donor mask 0x{_currentReplayPhase.StableDonorMask:X2} hid {gap} injectable slot(s)",
                    OracleGapCategory.FairnessOrdering =>
                        $"Credit-based fairness ordering skipped {gap} ready candidate(s)",
                    OracleGapCategory.LegalityConservatism =>
                        $"Safety mask or certificate conservatism rejected {gap} oracle-legal candidate(s)",
                    OracleGapCategory.DomainIsolation =>
                        $"Domain isolation enforcement blocked {gap} candidate(s)",
                    OracleGapCategory.SpeculationBudget =>
                        $"Speculation budget exhaustion prevented {gap} speculative injection(s)",
                    _ => $"Unclassified gap of {gap} slot(s)"
                };

                _counterexamples.Add(new OracleCounterexample(
                    TotalSchedulerCycles,
                    category,
                    gap,
                    description));
            }
        }

        /// <summary>
        /// Classify the primary reason for oracle gap based on current scheduler state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private OracleGapCategory ClassifyGapCategory(
            ShadowOracleCycleSnapshot cycleSnapshot,
            int ownerVirtualThreadId)
        {
            // Priority order: donor restriction > speculation budget > domain isolation > fairness > legality

            // If replay phase is active and donor mask restricts slots, donor restriction is primary
            if (_currentReplayPhase.IsActive && _currentReplayPhase.HasStableDonorStructure)
            {
                byte donorMask = _currentReplayPhase.StableDonorMask;
                if (donorMask != 0xFF)
                    return OracleGapCategory.DonorRestriction;
            }

            // If speculation budget was exhausted this cycle
            if (SpeculationBudgetEnabled && _speculationBudget <= 0)
                return OracleGapCategory.SpeculationBudget;

            // If domain isolation blocked candidates recently
            if (DomainIsolationBlockedAttempts > 0 &&
                _runtimeLegalityService.IsKernelDomainIsolationEnabled)
            {
                // Check if any pending candidate has domain isolation issues
                for (int vt = 0; vt < SMT_WAYS; vt++)
                {
                    if (vt == ownerVirtualThreadId) continue;
                    if (cycleSnapshot.NominationSnapshot.TryGetCandidate(vt, out MicroOp candidate) &&
                        candidate.Placement.DomainTag == 0)
                        return OracleGapCategory.DomainIsolation;
                }
            }

            // If fairness credit ordering was active
            if (CreditFairnessEnabled)
                return OracleGapCategory.FairnessOrdering;

            return OracleGapCategory.LegalityConservatism;
        }
    }
}
