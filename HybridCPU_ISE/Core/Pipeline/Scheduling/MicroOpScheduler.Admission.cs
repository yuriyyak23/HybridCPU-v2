using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {
        //  Phase 03: Two-Stage Admission Pipeline
        // ══════════════════════════════════════════════════════════════

        // ── Phase 06: Last certificate reject detail for diagnostics ──

        /// <summary>
        /// Last certificate-layer detail captured by the most recent
        /// <see cref="LegalityDecision"/> consumed by <see cref="TryClassAdmission"/>.
        /// Reset each admission attempt.
        /// <para>HLS: 2-bit diagnostic register — not on critical path.</para>
        /// </summary>
        private CertificateRejectDetail _lastCertificateRejectDetail;

        /// <summary>
        /// Last checker-owned reject kind captured by the most recent SMT legality
        /// decision consumed by typed-slot admission.
        /// </summary>
        private RejectKind _lastSmtLegalityRejectKind;

        /// <summary>
        /// Last checker-owned authority source captured by the most recent SMT
        /// legality decision consumed by typed-slot admission.
        /// </summary>
        private LegalityAuthoritySource _lastSmtLegalityAuthoritySource;

        /// <summary>
        /// Last <see cref="TypedSlotRejectClassification"/> produced by
        /// <see cref="RecordTypedSlotReject(TypedSlotRejectReason, MicroOp)"/>.
        /// Available for Phase 08 telemetry and diagnostics.
        /// <para>HLS: diagnostic register bank — not on critical path.</para>
        /// </summary>
        private TypedSlotRejectClassification _lastRejectClassification;

        /// <summary>
        /// Stage A: check whether the candidate is admissible at class level.
        /// This stage does not select a physical lane. It only decides whether the
        /// operation may advance to Stage B under the current capacity, legality,
        /// and dynamic-state envelope.
        /// <para>
        /// Gate order: A1 (class-capacity, O(1)) → A2 (explicit legality decision, O(128-bit AND))
        /// → A3 (outer-cap dynamic gates, O(8 comparators)). Fail-fast from cheap to expensive.
        /// </para>
        /// <para>
        /// Gate A2 consumes <see cref="IRuntimeLegalityService.EvaluateSmtLegality(ReplayPhaseContext, BundleResourceCertificate4Way, SmtBundleMetadata4Way, BoundaryGuardState, MicroOp)"/>
        /// and preserves <see cref="CertificateRejectDetail"/> via the returned <see cref="LegalityDecision"/>.
        /// The legality-service substage is itself ordered guard-plane first, then
        /// replay/certificate authority.
        /// </para>
        /// <para>HLS: ~3 LUT layers combinational (fits in SCHED1 pipeline stage).</para>
        /// </summary>
        /// <param name="candidate">Candidate micro-operation.</param>
        /// <param name="cert4Way">Current bundle certificate (ref for read-only access).</param>
        /// <param name="bundleMetadata">Explicit runtime bundle metadata transport.</param>
        /// <param name="boundaryGuard">Explicit runtime boundary guard transport.</param>
        /// <param name="ownerVirtualThreadId">Bundle owner VT (for speculation budget check).</param>
        /// <param name="currentPassInjections">Number of successful injections so far in this pass.</param>
        /// <param name="rejectReason">Output: reason for rejection, if any.</param>
        /// <returns><see langword="true"/> if class-admitted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryClassAdmission(
            MicroOp candidate,
            ref BundleResourceCertificate4Way cert4Way,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            ref ClassTemplateAdmissionState templateState,
            int ownerVirtualThreadId,
            int currentPassInjections,
            out TypedSlotRejectReason rejectReason)
        {
            rejectReason = TypedSlotRejectReason.None;
            _lastCertificateRejectDetail = CertificateRejectDetail.None;
            _lastSmtLegalityRejectKind = RejectKind.None;
            _lastSmtLegalityAuthoritySource = LegalityAuthoritySource.StructuralCertificate;

            // A1. Class-capacity (Phase 02 data).
            // Phase 07: Template-guided fast path — check remaining template budget first.
            // If template budget has remaining capacity for this class, skip full HasFreeCapacity check.
            // Unclassified ops bypass class-capacity (legacy path: no typed lanes).
            if (candidate.Placement.RequiredSlotClass != SlotClass.Unclassified)
            {
                bool capacityOk = false;
                if (templateState.TryConsumeFastPathBudget(candidate.Placement.RequiredSlotClass))
                {
                    TypedSlotFastPathAccepts++;
                    capacityOk = true;
                }

                if (!capacityOk && !_classCapacity.HasFreeCapacity(candidate.Placement.RequiredSlotClass))
                {
                    rejectReason = currentPassInjections == 0
                        ? TypedSlotRejectReason.StaticClassOvercommit
                        : TypedSlotRejectReason.DynamicClassExhaustion;
                    return false;
                }
            }

            // A2. Legality service decision.
            // Phase 05: scheduler now consumes an explicit LegalityDecision from the
            // runtime-local legality service instead of interpreting certificate/mask-shaped
            // legality artifacts directly.
            LegalityDecision legalityDecision = EvaluateSmtLegality(
                cert4Way,
                bundleMetadata,
                boundaryGuard,
                candidate);
            _lastCertificateRejectDetail = legalityDecision.CertificateDetail;
            _lastSmtLegalityRejectKind = legalityDecision.RejectKind;
            _lastSmtLegalityAuthoritySource = legalityDecision.AuthoritySource;
            if (!legalityDecision.IsAllowed)
            {
                // Scheduler-visible typed-slot taxonomy intentionally collapses
                // checker-owned guard/certificate denials to ResourceConflict.
                // Fine-grained cause remains in _lastSmtLegalityRejectKind and
                // _lastSmtLegalityAuthoritySource for diagnostics.
                rejectReason = TypedSlotRejectReason.ResourceConflict;
                return false;
            }

            // A3. IsOuterCapBlocking — consolidates SuppressLsu, projected scoreboard
            // saturation, bank-pending, sampled memory budget, and speculation budget.
            if (candidate is not AssistMicroOp)
            {
                int candidateBankId = candidate is LoadStoreMicroOp ls ? ls.MemoryBankId : -1;
                if (!TryPassOuterCap(candidate, candidate.VirtualThreadId,
                                     candidate.IsMemoryOp, candidateBankId, ownerVirtualThreadId, out rejectReason))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Stage B: materialize one concrete physical lane for an admitted candidate.
        /// Called only after <see cref="TryClassAdmission"/> returns <see langword="true"/>.
        /// <para>
        /// HardPinned ops use <see cref="MicroOp.Placement"/>.<see cref="SlotPlacementMetadata.PinnedLaneId"/> directly.
        /// ClassFlexible ops intersect the class lane mask with free lanes
        /// and apply <see cref="DeterministicLaneChooser.SelectWithReplayHint"/>
        /// (Tier 1: lowest-free, Tier 2: replay lane reuse).
        /// </para>
        /// <para>
        /// Stage B does not widen legality. It only resolves the final lane from the
        /// already admitted class and replay/topology constraints.
        /// </para>
        /// <para>HLS: ~2 LUT layers combinational (fits in SCHED2 pipeline stage).</para>
        /// </summary>
        /// <param name="candidate">Class-admitted candidate.</param>
        /// <param name="bundleOccupancy">Bitmask of occupied physical slots (bit N → slot N occupied).</param>
        /// <param name="selectedLane">Output: chosen physical lane (0–7).</param>
        /// <param name="rejectReason">Output: reason for rejection, if any.</param>
        /// <returns><see langword="true"/> if a lane was successfully materialized.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryMaterializeLane(
            MicroOp candidate,
            byte bundleOccupancy,
            out int selectedLane,
            out TypedSlotRejectReason rejectReason)
        {
            rejectReason = TypedSlotRejectReason.None;
            selectedLane = -1;

            if (candidate.Placement.PinningKind == SlotPinningKind.HardPinned)
            {
                int lane = candidate.Placement.PinnedLaneId;
                if ((bundleOccupancy & (1 << lane)) != 0)
                {
                    rejectReason = TypedSlotRejectReason.PinnedLaneConflict;
                    return false;
                }
                selectedLane = lane;
                return true;
            }

            // ClassFlexible: intersect class lanes with free lanes (and replay mask).
            byte classMask = SlotClassLaneMap.GetLaneMask(candidate.Placement.RequiredSlotClass);
            byte freeLanes = (byte)(classMask & ~bundleOccupancy);

            // Apply replay-aware stable donor mask as additional filter.
            if (_currentReplayPhase.HasStableDonorStructure)
            {
                freeLanes &= _currentReplayPhase.StableDonorMask;
            }

            if (freeLanes == 0)
            {
                rejectReason = TypedSlotRejectReason.LateBindingConflict;
                return false;
            }

            // Phase 04: Deterministic lane selection with replay hint.
            selectedLane = DeterministicLaneChooser.SelectWithReplayHint(
                freeLanes,
                _currentReplayPhase.IsActive,
                GetPreviousPhaseLane(candidate.Placement.RequiredSlotClass));

            // Phase 04: Tier 2 telemetry tracking.
            if (_currentReplayPhase.IsActive)
            {
                int prevLane = GetPreviousPhaseLane(candidate.Placement.RequiredSlotClass);
                if (prevLane >= 0 && selectedLane == prevLane)
                    LaneReuseHits++;
                else if (prevLane >= 0)
                    LaneReuseMisses++;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetReplayAwareStealMask(byte stealMask, byte donorMask)
        {
            if (!_currentReplayPhase.HasStableDonorStructure)
            {
                return stealMask;
            }

            byte stableDonorMask = _currentReplayPhase.StableDonorMask;
            if (donorMask != 0)
            {
                stableDonorMask &= donorMask;
            }

            return (byte)(stealMask & stableDonorMask);
        }

        /// <summary>
        /// Legacy TDM-only ranking (used when CreditFairnessEnabled is false).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetRankedVirtualThread(int scanIndex, bool forceTdm)
        {
            return forceTdm ? ((scanIndex + 1) % SMT_WAYS) : scanIndex;
        }

        /// <summary>
        /// Phase 2A: Build credit-ranked VT order for the current scheduling cycle.
        /// Returns a fixed-size 4-element array sorted by descending credit, tie-break ascending vtId.
        /// HLS: 4-input comparator tree → 2-level MUX, ≤2 LUT depth.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetCreditRankedOrder(int ownerVirtualThreadId, Span<int> ranked)
        {
            // Simple insertion sort on 4 elements — deterministic, HLS-friendly.
            int count = 0;
            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                if (vt == ownerVirtualThreadId) continue;
                ranked[count++] = vt;
            }

            // Sort by descending credit, ascending vtId on tie.
            for (int i = 0; i < count - 1; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    int a = ranked[i];
                    int b = ranked[j];
                    if (_fairnessCredits[b] > _fairnessCredits[a] ||
                        (_fairnessCredits[b] == _fairnessCredits[a] && b < a))
                    {
                        ranked[i] = b;
                        ranked[j] = a;
                    }
                }
            }
        }

        /// <summary>
        /// Phase 2A: Accumulate credit for VTs that had valid candidates but were not injected this cycle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AccumulateFairnessCredits(
            SmtNominationState nominationState,
            int ownerVirtualThreadId,
            ReadOnlySpan<bool> wasInjected)
        {
            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                if (vt == ownerVirtualThreadId) continue;

                if (wasInjected[vt])
                {
                    // Spend one credit on successful injection
                    if (_fairnessCredits[vt] > 0)
                        _fairnessCredits[vt]--;
                }
                else if (nominationState.IsReadyCandidate(vt))
                {
                    // VT had a valid candidate but was not injected — accumulate credit
                    if (_fairnessCredits[vt] < FAIRNESS_CREDIT_CAP)
                        _fairnessCredits[vt]++;

                    if (_fairnessCredits[vt] >= FAIRNESS_CREDIT_CAP)
                        FairnessStarvationEvents++;
                }
            }
        }

        /// <summary>
        /// Prime the pack-local memory issue state from live scoreboard truth.
        /// This folds sampled widened load/store budget, bundle-local store-bank overlay,
        /// and projected per-VT outstanding-memory saturation into one deterministic pass.
        /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PrepareProjectedMemoryIssuePass(System.Collections.Generic.IReadOnlyList<MicroOp?> bundle)
            {
            _remainingHardwareMemoryIssueBudget = _hardwareOccupancySnapshot.MemoryIssueBudget;
            _consumedHardwareMemoryBudgetByBank = 0;
            _remainingHardwareLoadIssueBudget = _hardwareOccupancySnapshot.ReadIssueBudget;
            _remainingHardwareStoreIssueBudget = _hardwareOccupancySnapshot.WriteIssueBudget;
            _consumedHardwareLoadBudgetByBank = 0;
            _consumedHardwareStoreBudgetByBank = 0;
            _bundleLocalOutstandingStoreBankMask = 0;
            InitializeProjectedOutstandingMemoryState();

            if (bundle == null)
            {
                return;
            }

                for (int i = 0; i < bundle.Count; i++)
                {
                    ConsumeProjectedMemoryIssueStateIfNeeded(bundle[i]);
                }
            }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeProjectedOutstandingMemoryState()
        {
            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                int liveOutstandingMemoryCount = GetOutstandingMemoryCount(vt);
                int liveFreeScoreboardSlots = GetFreeSmtScoreboardSlotCount(vt);

                _projectedOutstandingMemoryCountByVt[vt] =
                    SaturateScoreboardCount(liveOutstandingMemoryCount);
                _projectedOutstandingMemoryCapacityByVt[vt] =
                    SaturateScoreboardCount(liveOutstandingMemoryCount + liveFreeScoreboardSlots);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasProjectedOutstandingMemoryCapacity(bool isMemoryOp, int virtualThreadId)
        {
            if (!isMemoryOp)
            {
                return true;
            }

            if ((uint)virtualThreadId >= SMT_WAYS)
            {
                return false;
            }

            return _projectedOutstandingMemoryCountByVt[virtualThreadId] <
                   _projectedOutstandingMemoryCapacityByVt[virtualThreadId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasHardwareMemoryBudget(MicroOp candidate, int memoryBankId)
        {
            if (candidate is not LoadStoreMicroOp)
            {
                return true;
            }

            return HasSharedHardwareMemoryBudget(memoryBankId) &&
                   HasDirectionalHardwareMemoryBudget(candidate, memoryBankId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConsumeProjectedMemoryIssueStateIfNeeded(MicroOp? candidate)
        {
            if (candidate is not LoadStoreMicroOp loadStoreCandidate)
            {
                return;
            }

            IncrementProjectedOutstandingMemoryCount(
                _projectedOutstandingMemoryCountByVt,
                loadStoreCandidate.VirtualThreadId);

            if (_remainingHardwareMemoryIssueBudget > 0)
            {
                _remainingHardwareMemoryIssueBudget--;
            }

            IncrementConsumedHardwareMemoryBudget(loadStoreCandidate.MemoryBankId);

            if (loadStoreCandidate is LoadMicroOp)
            {
                if (_remainingHardwareLoadIssueBudget > 0)
                {
                    _remainingHardwareLoadIssueBudget--;
                }

                IncrementConsumedHardwareLoadBudget(loadStoreCandidate.MemoryBankId);
            }
            else if (loadStoreCandidate is StoreMicroOp storeCandidate)
            {
                if (_remainingHardwareStoreIssueBudget > 0)
                {
                    _remainingHardwareStoreIssueBudget--;
                }

                IncrementConsumedHardwareStoreBudget(storeCandidate.MemoryBankId);
                MarkBundleLocalOutstandingStoreBank(storeCandidate.MemoryBankId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasSharedHardwareMemoryBudget(int memoryBankId)
        {
            if (_remainingHardwareMemoryIssueBudget == 0)
            {
                return false;
            }

            if ((uint)memoryBankId >= 16)
            {
                return true;
            }

            return GetConsumedHardwareMemoryBudget(memoryBankId) <
                   _hardwareOccupancySnapshot.GetMemoryBudgetForBank(memoryBankId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasDirectionalHardwareMemoryBudget(MicroOp candidate, int memoryBankId)
        {
            if (candidate is LoadMicroOp)
            {
                return HasDirectionalHardwareBudget(
                    _remainingHardwareLoadIssueBudget,
                    _consumedHardwareLoadBudgetByBank,
                    _hardwareOccupancySnapshot.GetReadBudgetForBank(memoryBankId),
                    memoryBankId);
            }

            if (candidate is StoreMicroOp)
            {
                return HasDirectionalHardwareBudget(
                    _remainingHardwareStoreIssueBudget,
                    _consumedHardwareStoreBudgetByBank,
                    _hardwareOccupancySnapshot.GetWriteBudgetForBank(memoryBankId),
                    memoryBankId);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasDirectionalHardwareBudget(
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte SaturateScoreboardCount(int value)
        {
            if (value <= 0)
            {
                return 0;
            }

            if (value >= SCOREBOARD_SLOTS)
            {
                return SCOREBOARD_SLOTS;
            }

            return (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IncrementProjectedOutstandingMemoryCount(
            byte[] projectedOutstandingCounts,
            int virtualThreadId)
        {
            if ((uint)virtualThreadId >= SMT_WAYS)
            {
                return;
            }

            if (projectedOutstandingCounts[virtualThreadId] < SCOREBOARD_SLOTS)
            {
                projectedOutstandingCounts[virtualThreadId]++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasBundleLocalOutstandingStoreBankPending(int bankId)
        {
            return (uint)bankId < 16 &&
                   (_bundleLocalOutstandingStoreBankMask & (1 << bankId)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MarkBundleLocalOutstandingStoreBank(int bankId)
        {
            if ((uint)bankId >= 16)
            {
                return;
            }

            _bundleLocalOutstandingStoreBankMask |= (ushort)(1 << bankId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetConsumedHardwareMemoryBudget(int bankId)
        {
            return GetPackedConsumedHardwareBudget(_consumedHardwareMemoryBudgetByBank, bankId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementConsumedHardwareMemoryBudget(int bankId)
        {
            _consumedHardwareMemoryBudgetByBank =
                IncrementPackedConsumedHardwareBudget(_consumedHardwareMemoryBudgetByBank, bankId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementConsumedHardwareLoadBudget(int bankId)
        {
            _consumedHardwareLoadBudgetByBank =
                IncrementPackedConsumedHardwareBudget(_consumedHardwareLoadBudgetByBank, bankId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementConsumedHardwareStoreBudget(int bankId)
        {
            _consumedHardwareStoreBudgetByBank =
                IncrementPackedConsumedHardwareBudget(_consumedHardwareStoreBudgetByBank, bankId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetPackedConsumedHardwareBudget(uint consumedPackedBudgetByBank, int bankId)
        {
            if ((uint)bankId >= 16)
            {
                return 0;
            }

            int shift = bankId * 2;
            return (byte)((consumedPackedBudgetByBank >> shift) & 0x3u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint IncrementPackedConsumedHardwareBudget(uint consumedPackedBudgetByBank, int bankId)
        {
            if ((uint)bankId >= 16)
            {
                return consumedPackedBudgetByBank;
            }

            int shift = bankId * 2;
            uint current = (consumedPackedBudgetByBank >> shift) & 0x3u;
            if (current >= 3)
            {
                return consumedPackedBudgetByBank;
            }

            uint next = current + 1u;
            return (consumedPackedBudgetByBank & ~(0x3u << shift)) | (next << shift);
        }

        /// <summary>
        /// Phase 2B: Compute bank pressure score for a memory-op candidate.
        /// Score = number of outstanding memory entries targeting the same bank across all VTs.
        /// Lower score = less contention = preferred.
        /// HLS: bank-indexed scan across 4×8 scoreboard entries.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBankPressureScore(int bankId)
        {
            if (bankId < 0) return 0;

            int score = 0;
            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                for (int s = 0; s < SCOREBOARD_SLOTS; s++)
                {
                    if (_smtScoreboard[vt, s] != -1 &&
                        _smtScoreboardBankId[vt, s] == bankId &&
                        (_smtScoreboardType[vt, s] == ScoreboardEntryType.OutstandingLoad ||
                         _smtScoreboardType[vt, s] == ScoreboardEntryType.OutstandingStore))
                    {
                        score++;
                    }
                }
            }

            return score;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryPassOuterCap(
            MicroOp candidate,
            int virtualThreadId,
            bool isMemoryOp,
            int memoryBankId,
            int bundleOwnerVtId,
            out TypedSlotRejectReason rejectReason)
        {
            rejectReason = TypedSlotRejectReason.None;

            if (SuppressLsu && isMemoryOp)
            {
                MemoryWallSuppressionsCount++;
                SmtRejectionsCount++;
                rejectReason = TypedSlotRejectReason.ScoreboardReject;
                return false;
            }

            if (!HasProjectedOutstandingMemoryCapacity(isMemoryOp, virtualThreadId))
            {
                MshrScoreboardStalls++;
                SmtRejectionsCount++;
                rejectReason = TypedSlotRejectReason.ScoreboardReject;
                return false;
            }

            if (isMemoryOp && memoryBankId >= 0 && IsBankPendingForVT(memoryBankId, virtualThreadId))
            {
                RecordBankPendingReject(memoryBankId);
                MshrScoreboardStalls++;
                SmtRejectionsCount++;
                rejectReason = TypedSlotRejectReason.BankPendingReject;
                return false;
            }

            if (candidate is StoreMicroOp && memoryBankId >= 0 &&
                HasBundleLocalOutstandingStoreBankPending(memoryBankId))
            {
                RecordBankPendingReject(memoryBankId);
                MshrScoreboardStalls++;
                SmtRejectionsCount++;
                rejectReason = TypedSlotRejectReason.BankPendingReject;
                return false;
            }

            if (!HasHardwareMemoryBudget(candidate, memoryBankId))
            {
                TypedSlotHardwareBudgetRejects++;
                SmtRejectionsCount++;
                rejectReason = TypedSlotRejectReason.HardwareBudgetReject;
                return false;
            }

            // Phase 2C: Speculation budget enforcement for cross-thread memory ops.
            // A candidate is speculative when it comes from a different VT than the bundle owner.
            if (SpeculationBudgetEnabled && isMemoryOp && bundleOwnerVtId >= 0 && virtualThreadId != bundleOwnerVtId)
            {
                if (_speculationBudget <= 0)
                {
                    SpeculationBudgetExhaustionEvents++;
                    SmtRejectionsCount++;
                    rejectReason = TypedSlotRejectReason.SpeculationBudgetReject;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true when the bundle contains a serialising event
        /// (<see cref="Arch.SerializationClass.FullSerial"/> or
        /// <see cref="Arch.SerializationClass.VmxSerial"/>) that forbids cross-domain
        /// FSP injection at this bundle boundary (G33 — Deterministic Legality Alignment).
        /// <para>
        /// Published into the SMT guard-plane key; the verifier now owns the rejection.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSmtBundleBlockedByBoundaryGuard(
            BundleResourceCertificate4Way bundleCert,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            LegalityDecision boundaryDecision =
                _runtimeLegalityService.EvaluateSmtBoundaryGuard(
                    _currentReplayPhase,
                    bundleCert,
                    bundleMetadata,
                    boundaryGuard);
            if (boundaryDecision.IsAllowed)
                return false;

            if (boundaryDecision.RejectKind == RejectKind.Boundary)
            {
                SerializingBoundaryRejects++;
                SmtBoundaryGuardRejects++;
            }

            _lastCertificateRejectDetail = boundaryDecision.CertificateDetail;
            _lastSmtLegalityRejectKind = boundaryDecision.RejectKind;
            _lastSmtLegalityAuthoritySource = boundaryDecision.AuthoritySource;
            return true;
        }

        /// <summary>
        /// Evaluate SMT legality through the explicit verifier-owned decision seam and
        /// consume cache-owned replay telemetry without reinterpreting reuse semantics locally.
        /// <para>HLS: same hot-path cost as the old bool helpers; only the authority surface changed.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LegalityDecision EvaluateSmtLegality(
            BundleResourceCertificate4Way bundleCert,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            MicroOp candidate)
        {
            return _runtimeLegalityService.EvaluateSmtLegality(
                _currentReplayPhase,
                bundleCert,
                bundleMetadata,
                boundaryGuard,
                candidate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvalidatePhaseCertificateTemplates(
            ReplayPhaseInvalidationReason reason,
            bool invalidateInterCore = true,
            bool invalidateFourWay = true)
        {
            _runtimeLegalityService.Invalidate(
                reason,
                invalidateInterCore,
                invalidateFourWay);
        }

    }
}
