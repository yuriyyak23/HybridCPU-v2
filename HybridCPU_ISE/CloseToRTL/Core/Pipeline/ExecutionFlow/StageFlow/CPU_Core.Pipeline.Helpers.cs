using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Resolve the authoritative retire eligibility mask for the current write-back window.
            /// Packet-local retire currently includes all occupied non-faulted scalar lanes, the
            /// already-live widened LSU subset on lanes 4..5, and the live lane-7 singleton carrier.
            /// Reference fallback preserves conservative single-lane drain when explicit packet lanes are not active.
            /// </summary>
            public static byte ResolveRetireEligibleWriteBackLanes(in WriteBackStage writeBackStage)
            {
                if (!writeBackStage.Valid)
                    return 0;

                byte occupiedMask = GetOccupiedRetireEligibleWriteBackLaneMask(writeBackStage);
                if (occupiedMask == 0)
                    return 0;

                if (!writeBackStage.UsesExplicitPacketLanes && writeBackStage.RetainsReferenceSequentialPath)
                {
                    byte retireLaneIndex = ResolveConservativeRetireEligibleLaneIndex(writeBackStage, occupiedMask);
                    if (retireLaneIndex >= 4)
                        return 0;

                    return (byte)(1 << retireLaneIndex);
                }

                byte faultMask = ResolveWriteBackFaultMask(writeBackStage);
                return (byte)(occupiedMask & ~faultMask);
            }

            /// <summary>
            /// Resolve a stable retire order from the current precise-retire eligibility mask.
            /// Ordering is deterministic and follows bundle slot order for explicit-packet lanes,
            /// with lane index used only as a stable tie-breaker or on the legacy path.
            /// </summary>
            public static int ResolveStableRetireOrder(in WriteBackStage writeBackStage, Span<byte> retireOrder)
            {
                byte eligibleMask = ResolveRetireEligibleWriteBackLanes(writeBackStage);
                int count = 0;

                for (byte laneIndex = 0; laneIndex < 8 && count < retireOrder.Length; laneIndex++)
                {
                    if (!IsRetireAuthoritativeWriteBackLane(writeBackStage, laneIndex))
                        continue;

                    if ((eligibleMask & (1 << laneIndex)) == 0)
                        continue;

                    InsertWriteBackLaneInStableRetireOrder(
                        writeBackStage,
                        retireOrder,
                        ref count,
                        laneIndex);
                }

                return count;
            }

            /// <summary>
            /// Determine whether the specified live retire-eligible lane is the precise retire candidate
            /// for the current write-back window.
            /// </summary>
            public static bool CanRetireLanePrecisely(in WriteBackStage writeBackStage, byte laneIndex)
            {
                if (!IsRetireAuthoritativeWriteBackLane(writeBackStage, laneIndex))
                    return false;

                return (ResolveRetireEligibleWriteBackLanes(writeBackStage) & (1 << laneIndex)) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsRetireAuthoritativeWriteBackLane(byte laneIndex)
                => laneIndex < 6 || laneIndex == 7;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsRetireAuthoritativeWriteBackLane(in WriteBackStage writeBackStage, byte laneIndex)
            {
                if (!IsRetireAuthoritativeWriteBackLane(laneIndex))
                    return false;

                ScalarWriteBackLaneState lane = writeBackStage.GetLane(laneIndex);
                return !lane.IsOccupied || lane.MicroOp == null || lane.MicroOp.IsRetireVisible;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsArchitecturallyInvisibleWriteBackLane(in ScalarWriteBackLaneState lane)
            {
                return lane.IsOccupied &&
                    lane.MicroOp != null &&
                    !lane.MicroOp.IsRetireVisible;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetOccupiedRetireEligibleWriteBackLaneMask(in WriteBackStage writeBackStage)
            {
                byte occupiedMask = 0;

                if (writeBackStage.Lane0.IsOccupied && IsRetireAuthoritativeWriteBackLane(writeBackStage, 0))
                    occupiedMask |= 1 << 0;

                if (writeBackStage.Lane1.IsOccupied && IsRetireAuthoritativeWriteBackLane(writeBackStage, 1))
                    occupiedMask |= 1 << 1;

                if (writeBackStage.Lane2.IsOccupied && IsRetireAuthoritativeWriteBackLane(writeBackStage, 2))
                    occupiedMask |= 1 << 2;

                if (writeBackStage.Lane3.IsOccupied && IsRetireAuthoritativeWriteBackLane(writeBackStage, 3))
                    occupiedMask |= 1 << 3;

                if (writeBackStage.Lane4.IsOccupied && IsRetireAuthoritativeWriteBackLane(writeBackStage, 4))
                    occupiedMask |= 1 << 4;

                if (writeBackStage.Lane5.IsOccupied && IsRetireAuthoritativeWriteBackLane(writeBackStage, 5))
                    occupiedMask |= 1 << 5;

                if (writeBackStage.Lane7.IsOccupied && IsRetireAuthoritativeWriteBackLane(writeBackStage, 7))
                    occupiedMask |= 1 << 7;

                return occupiedMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CountOccupiedScalarWriteBackLanes(in WriteBackStage writeBackStage)
            {
                int count = 0;

                if (writeBackStage.Lane0.IsOccupied)
                    count++;

                if (writeBackStage.Lane1.IsOccupied)
                    count++;

                if (writeBackStage.Lane2.IsOccupied)
                    count++;

                if (writeBackStage.Lane3.IsOccupied)
                    count++;

                return count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveConservativeRetireEligibleLaneIndex(in WriteBackStage writeBackStage, byte occupiedMask)
            {
                if (writeBackStage.UsesExplicitPacketLanes &&
                    TryResolveOldestOrderedWriteBackLaneIndex(
                        writeBackStage,
                        occupiedMask,
                        out byte explicitPacketLaneIndex))
                {
                    return explicitPacketLaneIndex;
                }

                if (IsRetireAuthoritativeWriteBackLane(writeBackStage.ActiveLaneIndex))
                {
                    byte activeLaneMask = (byte)(1 << writeBackStage.ActiveLaneIndex);
                    if ((occupiedMask & activeLaneMask) != 0)
                        return writeBackStage.ActiveLaneIndex;
                }

                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    if (!IsRetireAuthoritativeWriteBackLane(writeBackStage, laneIndex))
                        continue;

                    if ((occupiedMask & (1 << laneIndex)) != 0)
                        return laneIndex;
                }

                return byte.MaxValue;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void InsertWriteBackLaneInStableRetireOrder(
                in WriteBackStage writeBackStage,
                Span<byte> retireOrder,
                ref int count,
                byte laneIndex)
            {
                int insertIndex = count;
                while (insertIndex > 0 &&
                    CompareWriteBackLaneOrder(
                        writeBackStage,
                        laneIndex,
                        retireOrder[insertIndex - 1]) < 0)
                {
                    retireOrder[insertIndex] = retireOrder[insertIndex - 1];
                    insertIndex--;
                }

                retireOrder[insertIndex] = laneIndex;
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveOldestOrderedWriteBackLaneIndex(
                in WriteBackStage writeBackStage,
                byte laneMask,
                out byte laneIndex)
            {
                laneIndex = byte.MaxValue;
                bool found = false;

                for (byte candidateLaneIndex = 0; candidateLaneIndex < 8; candidateLaneIndex++)
                {
                    if (!IsRetireAuthoritativeWriteBackLane(writeBackStage, candidateLaneIndex))
                        continue;

                    if ((laneMask & (1 << candidateLaneIndex)) == 0)
                        continue;

                    if (!found ||
                        CompareWriteBackLaneOrder(
                            writeBackStage,
                            candidateLaneIndex,
                            laneIndex) < 0)
                    {
                        laneIndex = candidateLaneIndex;
                        found = true;
                    }
                }

                return found;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CompareWriteBackLaneOrder(
                in WriteBackStage writeBackStage,
                byte leftLaneIndex,
                byte rightLaneIndex)
            {
                if (leftLaneIndex == rightLaneIndex)
                    return 0;

                if (TryResolveWriteBackLaneOrderKey(
                        writeBackStage,
                        leftLaneIndex,
                        out int leftOrderKey) &&
                    TryResolveWriteBackLaneOrderKey(
                        writeBackStage,
                        rightLaneIndex,
                        out int rightOrderKey))
                {
                    int compareResult = leftOrderKey.CompareTo(rightOrderKey);
                    if (compareResult != 0)
                        return compareResult;
                }

                return leftLaneIndex.CompareTo(rightLaneIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveWriteBackLaneOrderKey(
                in WriteBackStage writeBackStage,
                byte laneIndex,
                out int orderKey)
            {
                orderKey = laneIndex;
                if (!writeBackStage.UsesExplicitPacketLanes)
                {
                    return false;
                }

                ScalarWriteBackLaneState lane = writeBackStage.GetLane(laneIndex);
                if (!lane.IsOccupied || lane.SlotIndex >= 8)
                    return false;

                orderKey = lane.SlotIndex;
                return true;
            }

            /// <summary>
            /// Resolve the authoritative control-flow scalar lane for the current execute window.
            /// Priority 5 slice 2 keeps control-flow dominance conservative: at most one occupied control-flow lane may redirect the pipeline.
            /// Only occupied materialized execute lanes with control-flow opcodes participate.
            /// </summary>
            public static byte ResolveAuthoritativeControlFlowScalarLanes(in ExecuteStage executeStage)
            {
                if (!executeStage.Valid)
                    return 0;

                byte occupiedControlFlowMask = GetOccupiedControlFlowExecuteLaneMask(executeStage);
                if (occupiedControlFlowMask == 0)
                    return 0;

                byte authoritativeLaneIndex = ResolveConservativeControlFlowLaneIndex(executeStage, occupiedControlFlowMask);
                if (authoritativeLaneIndex >= 4)
                    return 0;

                return (byte)(1 << authoritativeLaneIndex);
            }

            /// <summary>
            /// Resolve a stable control-flow dominance order for the current execute window.
            /// Ordering is deterministic and lane-index ordered within the current dominance window.
            /// </summary>
            public static int ResolveStableControlFlowOrder(in ExecuteStage executeStage, Span<byte> controlFlowOrder)
            {
                byte authoritativeMask = ResolveAuthoritativeControlFlowScalarLanes(executeStage);
                int count = 0;

                for (byte laneIndex = 0; laneIndex < 4 && count < controlFlowOrder.Length; laneIndex++)
                {
                    if ((authoritativeMask & (1 << laneIndex)) == 0)
                        continue;

                    controlFlowOrder[count++] = laneIndex;
                }

                return count;
            }

            /// <summary>
            /// Resolve dominated scalar peer lanes for the current authoritative control-flow winner.
            /// Priority 5 slice 2 conservatively treats every other occupied peer in the same execute window as dominated when a redirect becomes authoritative.
            /// </summary>
            public static byte ResolveDominatedScalarLanesForControlFlow(in ExecuteStage executeStage)
            {
                byte authoritativeMask = ResolveAuthoritativeControlFlowScalarLanes(executeStage);
                if (authoritativeMask == 0)
                    return 0;

                byte occupiedMask = GetOccupiedScalarExecuteLaneMask(executeStage);
                return (byte)(occupiedMask & ~authoritativeMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetOccupiedScalarExecuteLaneMask(in ExecuteStage executeStage)
            {
                byte occupiedMask = 0;

                if (executeStage.Lane0.IsOccupied)
                    occupiedMask |= 1 << 0;

                if (executeStage.Lane1.IsOccupied)
                    occupiedMask |= 1 << 1;

                if (executeStage.Lane2.IsOccupied)
                    occupiedMask |= 1 << 2;

                if (executeStage.Lane3.IsOccupied)
                    occupiedMask |= 1 << 3;

                return occupiedMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CountOccupiedScalarExecuteLanes(in ExecuteStage executeStage)
            {
                int count = 0;

                if (executeStage.Lane0.IsOccupied)
                    count++;

                if (executeStage.Lane1.IsOccupied)
                    count++;

                if (executeStage.Lane2.IsOccupied)
                    count++;

                if (executeStage.Lane3.IsOccupied)
                    count++;

                return count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetOccupiedControlFlowExecuteLaneMask(in ExecuteStage executeStage)
            {
                byte occupiedMask = 0;

                if (executeStage.Lane0.IsOccupied && IsControlFlowOpcode(executeStage.Lane0.OpCode))
                    occupiedMask |= 1 << 0;

                if (executeStage.Lane1.IsOccupied && IsControlFlowOpcode(executeStage.Lane1.OpCode))
                    occupiedMask |= 1 << 1;

                if (executeStage.Lane2.IsOccupied && IsControlFlowOpcode(executeStage.Lane2.OpCode))
                    occupiedMask |= 1 << 2;

                if (executeStage.Lane3.IsOccupied && IsControlFlowOpcode(executeStage.Lane3.OpCode))
                    occupiedMask |= 1 << 3;

                return occupiedMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveConservativeControlFlowLaneIndex(in ExecuteStage executeStage, byte occupiedControlFlowMask)
            {
                if (executeStage.ActiveLaneIndex < 4)
                {
                    byte activeLaneMask = (byte)(1 << executeStage.ActiveLaneIndex);
                    if ((occupiedControlFlowMask & activeLaneMask) != 0)
                        return executeStage.ActiveLaneIndex;
                }

                for (byte laneIndex = 0; laneIndex < 4; laneIndex++)
                {
                    if ((occupiedControlFlowMask & (1 << laneIndex)) != 0)
                        return laneIndex;
                }

                return byte.MaxValue;
            }

            /// <summary>
            /// Resolve mixed-peer control-flow dominance for the current execute window.
            /// Considers all control-flow opcodes (Call, Return, Interrupt, InterruptReturn, Jump variants)
            /// with younger occupied non-control-flow peers. The authoritative redirect winner suppresses
            /// all younger occupied peers deterministically.
            /// </summary>
            public static byte ResolveControlFlowDominanceForMixedPeers(in ExecuteStage executeStage)
            {
                byte authoritativeMask = ResolveAuthoritativeControlFlowScalarLanes(executeStage);
                if (authoritativeMask == 0)
                    return 0;

                byte occupiedMask = GetOccupiedScalarExecuteLaneMask(executeStage);
                byte dominatedPeers = (byte)(occupiedMask & ~authoritativeMask);

                return dominatedPeers;
            }

            /// <summary>
            /// Determine whether a younger peer can commit after the authoritative redirect winner has been selected.
            /// Priority 5 contract: no younger scalar peer may commit after an authoritative redirect winner.
            /// </summary>
            public static bool CanYoungerPeerCommitAfterRedirectWinner(in ExecuteStage executeStage, byte peerLaneIndex)
            {
                if (peerLaneIndex >= 4)
                    return false;

                byte authoritativeMask = ResolveAuthoritativeControlFlowScalarLanes(executeStage);
                if (authoritativeMask == 0)
                    return true;

                byte peerBit = (byte)(1 << peerLaneIndex);
                if ((authoritativeMask & peerBit) != 0)
                    return true;

                ScalarExecuteLaneState peer = executeStage.GetLane(peerLaneIndex);
                if (!peer.IsOccupied)
                    return false;

                return false;
            }

            /// <summary>
            /// Determine whether the selected non-scalar slot mask contaminates retire eligibility.
            /// Priority 5 contract: selected non-scalar slots outside the live widened retire subset
            /// must never create extra retire eligibility beyond what materially occupied retire lanes provide.
            /// </summary>
            public static bool DoesSelectedNonScalarSlotMaskContaminateRetireEligibility(
                in WriteBackStage writeBackStage)
            {
                byte nonScalarMask = writeBackStage.SelectedNonScalarSlotMask;
                if (nonScalarMask == 0)
                    return false;

                byte occupiedMask = GetOccupiedRetireEligibleWriteBackLaneMask(writeBackStage);
                byte nonScalarOnlyBits = (byte)(nonScalarMask & ~occupiedMask);

                byte eligibleMask = ResolveRetireEligibleWriteBackLanes(writeBackStage);
                return (eligibleMask & nonScalarOnlyBits) != 0;
            }

            /// <summary>
            /// Stage 6 Phase D: build the union set of register write IDs across all occupied
            /// scalar lanes in the issue packet. Uses the authoritative runtime admission snapshot
            /// for each lane's slot rather than rereading residual carrier write-register arrays.
            /// Returns the set as a <see cref="HashSet{T}"/> for O(1) conflict lookup.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static HashSet<int> ResolveScalarLaneWriteRegisterSet(
                in Core.BundleIssuePacket issuePacket,
                IReadOnlyList<Core.DecodedBundleSlotDescriptor> bundleSlots)
            {
                HashSet<int> writeSet = new();

                for (byte laneIndex = 0; laneIndex < 4; laneIndex++)
                {
                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (!lane.IsOccupied)
                        continue;

                    byte slotIndex = lane.SlotIndex;
                    if (slotIndex >= 8 || bundleSlots == null || slotIndex >= bundleSlots.Count)
                        continue;

                    Core.DecodedBundleSlotDescriptor laneSlot = bundleSlots[slotIndex];
                    if (!laneSlot.IsValid)
                        continue;

                    IReadOnlyList<int> writeRegs = laneSlot.GetRuntimeAdmissionWriteRegisters();
                    if (writeRegs == null)
                        continue;

                    for (int i = 0; i < writeRegs.Count; i++)
                        writeSet.Add(writeRegs[i]);
                }

                return writeSet;
            }

            /// <summary>
            /// Stage 6 Phase D: resolve the mask of selected non-scalar slots that can co-issue with the
            /// current scalar packet without register write conflicts.
            /// <para>
            /// Policy: a selected non-scalar slot is eligible for co-issue when its register write set
            /// does not overlap with the union of all scalar lanes' register write sets.
            /// Slot-level overlap (same bundle slot used by both scalar and non-scalar selections) is
            /// structurally impossible — the decoder separates them — but is checked defensively.
            /// </para>
            /// <para>
            /// This is a <b>policy-only</b> method. It does not trigger actual co-execution;
            /// it returns the mask for later phases to consume.
            /// </para>
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static byte ResolveSelectedNonScalarCoexistenceMask(
                in Core.BundleIssuePacket issuePacket,
                IReadOnlyList<Core.DecodedBundleSlotDescriptor> bundleSlots,
                ref int conflictCount)
            {
                byte selectedNonScalarMask = issuePacket.SelectedNonScalarSlotMask;
                if (selectedNonScalarMask == 0)
                    return 0;

                byte scalarSlotMask = 0;
                for (byte laneIndex = 0; laneIndex < 4; laneIndex++)
                {
                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (lane.IsOccupied && lane.SlotIndex < 8)
                        scalarSlotMask |= (byte)(1 << lane.SlotIndex);
                }

                HashSet<int> scalarWriteSet = ResolveScalarLaneWriteRegisterSet(issuePacket, bundleSlots);
                byte coexistenceMask = 0;

                for (byte slotIndex = 0; slotIndex < 8; slotIndex++)
                {
                    byte slotBit = (byte)(1 << slotIndex);
                    if ((selectedNonScalarMask & slotBit) == 0)
                        continue;

                    if ((scalarSlotMask & slotBit) != 0)
                    {
                        conflictCount++;
                        continue;
                    }

                    if (bundleSlots == null || slotIndex >= bundleSlots.Count)
                    {
                        conflictCount++;
                        continue;
                    }

                    Core.DecodedBundleSlotDescriptor nonScalarSlot = bundleSlots[slotIndex];
                    if (!nonScalarSlot.IsValid)
                        continue;

                    bool hasConflict = false;
                    IReadOnlyList<int> nonScalarWriteRegs = nonScalarSlot.GetRuntimeAdmissionWriteRegisters();
                    if (nonScalarWriteRegs != null && scalarWriteSet.Count > 0)
                    {
                        for (int i = 0; i < nonScalarWriteRegs.Count; i++)
                        {
                            if (scalarWriteSet.Contains(nonScalarWriteRegs[i]))
                            {
                                hasConflict = true;
                                break;
                            }
                        }
                    }

                    if (hasConflict)
                    {
                        conflictCount++;
                    }
                    else
                    {
                        coexistenceMask |= slotBit;
                    }
                }

                return coexistenceMask;
            }

            /// <summary>
            /// Resolve retire-eligible live write-back lanes excluding any lanes that carry a materialized fault.
            /// Faulted lanes are not eligible for normal retire; they must be delivered via the exception path.
            /// </summary>
            public static byte ResolveRetireEligibleWriteBackLanesExcludingFaulted(in WriteBackStage writeBackStage)
            {
                byte eligibleMask = ResolveRetireEligibleWriteBackLanes(writeBackStage);
                byte faultMask = ResolveWriteBackFaultMask(writeBackStage);

                return (byte)(eligibleMask & ~faultMask);
            }

            /// <summary>
            /// Resolve the deterministic stage-aware exception order for the current materialized fault window.
            /// Stage priority is `WB` then `MEM` then `EX`, and lane order within a stage is ascending lane index.
            /// The input masks must be derived only from authoritative materialized lane state.
            /// </summary>
            public static int ResolveStageAwareExceptionOrder(
                byte writeBackFaultMask,
                byte memoryFaultMask,
                byte executeFaultMask,
                Span<PipelineStage> orderedStages,
                Span<byte> orderedLanes)
            {
                int count = 0;

                count = AppendStageAwareExceptionOrder(PipelineStage.WriteBack, writeBackFaultMask, orderedStages, orderedLanes, count);
                count = AppendStageAwareExceptionOrder(PipelineStage.Memory, memoryFaultMask, orderedStages, orderedLanes, count);
                count = AppendStageAwareExceptionOrder(PipelineStage.Execute, executeFaultMask, orderedStages, orderedLanes, count);

                return count;
            }

            /// <summary>
            /// Resolve the authoritative stage-aware exception winner from fixed-width materialized fault masks.
            /// Returns the oldest deterministic winner according to `WB` → `MEM` → `EX`, then ascending lane index.
            /// </summary>
            public static bool TryResolveStageAwareExceptionWinner(
                byte writeBackFaultMask,
                byte memoryFaultMask,
                byte executeFaultMask,
                out PipelineStage winnerStage,
                out byte winnerLaneIndex)
            {
                Span<PipelineStage> orderedStages = stackalloc PipelineStage[24];
                Span<byte> orderedLanes = stackalloc byte[24];
                int orderCount = ResolveStageAwareExceptionOrder(
                    writeBackFaultMask,
                    memoryFaultMask,
                    executeFaultMask,
                    orderedStages,
                    orderedLanes);

                if (orderCount == 0)
                {
                    winnerStage = PipelineStage.None;
                    winnerLaneIndex = byte.MaxValue;
                    return false;
                }

                winnerStage = orderedStages[0];
                winnerLaneIndex = orderedLanes[0];
                return true;
            }

            /// <summary>
            /// Resolve the authoritative execute-stage materialized fault mask from lane state.
            /// Only occupied lanes with a materialized fault carrier participate.
            /// </summary>
            public static byte ResolveExecuteFaultMask(in ExecuteStage executeStage)
            {
                byte faultMask = 0;

                if (executeStage.Lane0.IsOccupied && executeStage.Lane0.HasFault)
                    faultMask |= 1 << 0;

                if (executeStage.Lane1.IsOccupied && executeStage.Lane1.HasFault)
                    faultMask |= 1 << 1;

                if (executeStage.Lane2.IsOccupied && executeStage.Lane2.HasFault)
                    faultMask |= 1 << 2;

                if (executeStage.Lane3.IsOccupied && executeStage.Lane3.HasFault)
                    faultMask |= 1 << 3;

                if (executeStage.Lane4.IsOccupied && executeStage.Lane4.HasFault)
                    faultMask |= 1 << 4;

                if (executeStage.Lane5.IsOccupied && executeStage.Lane5.HasFault)
                    faultMask |= 1 << 5;

                if (executeStage.Lane6.IsOccupied && executeStage.Lane6.HasFault)
                    faultMask |= 1 << 6;

                if (executeStage.Lane7.IsOccupied && executeStage.Lane7.HasFault)
                    faultMask |= 1 << 7;

                return faultMask;
            }

            /// <summary>
            /// Resolve the authoritative memory-stage materialized fault mask from lane state.
            /// Only occupied lanes with a materialized fault carrier participate.
            /// </summary>
            public static byte ResolveMemoryFaultMask(in MemoryStage memoryStage)
            {
                byte faultMask = 0;

                if (memoryStage.Lane0.IsOccupied && memoryStage.Lane0.HasFault)
                    faultMask |= 1 << 0;

                if (memoryStage.Lane1.IsOccupied && memoryStage.Lane1.HasFault)
                    faultMask |= 1 << 1;

                if (memoryStage.Lane2.IsOccupied && memoryStage.Lane2.HasFault)
                    faultMask |= 1 << 2;

                if (memoryStage.Lane3.IsOccupied && memoryStage.Lane3.HasFault)
                    faultMask |= 1 << 3;

                if (memoryStage.Lane4.IsOccupied && memoryStage.Lane4.HasFault)
                    faultMask |= 1 << 4;

                if (memoryStage.Lane5.IsOccupied && memoryStage.Lane5.HasFault)
                    faultMask |= 1 << 5;

                if (memoryStage.Lane6.IsOccupied && memoryStage.Lane6.HasFault)
                    faultMask |= 1 << 6;

                if (memoryStage.Lane7.IsOccupied && memoryStage.Lane7.HasFault)
                    faultMask |= 1 << 7;

                return faultMask;
            }

            /// <summary>
            /// Resolve the authoritative write-back-stage materialized fault mask from lane state.
            /// Only occupied lanes with a materialized fault carrier participate.
            /// </summary>
            public static byte ResolveWriteBackFaultMask(in WriteBackStage writeBackStage)
            {
                byte faultMask = 0;

                if (writeBackStage.Lane0.IsOccupied && writeBackStage.Lane0.HasFault)
                    faultMask |= 1 << 0;

                if (writeBackStage.Lane1.IsOccupied && writeBackStage.Lane1.HasFault)
                    faultMask |= 1 << 1;

                if (writeBackStage.Lane2.IsOccupied && writeBackStage.Lane2.HasFault)
                    faultMask |= 1 << 2;

                if (writeBackStage.Lane3.IsOccupied && writeBackStage.Lane3.HasFault)
                    faultMask |= 1 << 3;

                if (writeBackStage.Lane4.IsOccupied && writeBackStage.Lane4.HasFault)
                    faultMask |= 1 << 4;

                if (writeBackStage.Lane5.IsOccupied && writeBackStage.Lane5.HasFault)
                    faultMask |= 1 << 5;

                if (writeBackStage.Lane6.IsOccupied && writeBackStage.Lane6.HasFault)
                    faultMask |= 1 << 6;

                if (writeBackStage.Lane7.IsOccupied && writeBackStage.Lane7.HasFault)
                    faultMask |= 1 << 7;

                return faultMask;
            }

            /// <summary>
            /// Resolve the authoritative stage-aware exception winner directly from materialized stage lane state.
            /// The fault masks are derived only from occupied `WB`, `MEM`, and `EX` lanes.
            /// </summary>
            public static bool TryResolveStageAwareExceptionWinner(
                in WriteBackStage writeBackStage,
                in MemoryStage memoryStage,
                in ExecuteStage executeStage,
                out PipelineStage winnerStage,
                out byte winnerLaneIndex)
            {
                if (TryResolveOldestOrderedWriteBackFaultLane(writeBackStage, out winnerLaneIndex))
                {
                    winnerStage = PipelineStage.WriteBack;
                    return true;
                }

                if (TryResolveOldestOrderedMemoryFaultLane(memoryStage, out winnerLaneIndex))
                {
                    winnerStage = PipelineStage.Memory;
                    return true;
                }

                if (TryResolveOldestOrderedExecuteFaultLane(executeStage, out winnerLaneIndex))
                {
                    winnerStage = PipelineStage.Execute;
                    return true;
                }

                winnerStage = PipelineStage.None;
                winnerLaneIndex = byte.MaxValue;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int AppendStageAwareExceptionOrder(
                PipelineStage stage,
                byte faultMask,
                Span<PipelineStage> orderedStages,
                Span<byte> orderedLanes,
                int count)
            {
                for (byte laneIndex = 0; laneIndex < 8 && count < orderedStages.Length && count < orderedLanes.Length; laneIndex++)
                {
                    if ((faultMask & (1 << laneIndex)) == 0)
                        continue;

                    orderedStages[count] = stage;
                    orderedLanes[count] = laneIndex;
                    count++;
                }

                return count;
            }

            /// <summary>
            /// Metadata record describing the authoritative stage-aware exception winner.
            /// Used for end-to-end fault delivery policy and VT identity consistency checks.
            /// </summary>
            public readonly struct StageAwareExceptionWinnerMetadata
            {
                public PipelineStage WinnerStage { get; }
                public byte WinnerLaneIndex { get; }
                public int OwnerThreadId { get; }
                public int VirtualThreadId { get; }
                public ulong FaultAddress { get; }
                public bool FaultIsWrite { get; }
                public ulong PC { get; }

                public StageAwareExceptionWinnerMetadata(
                    PipelineStage winnerStage,
                    byte winnerLaneIndex,
                    int ownerThreadId,
                    int virtualThreadId,
                    ulong faultAddress,
                    bool faultIsWrite,
                    ulong pc)
                {
                    WinnerStage = winnerStage;
                    WinnerLaneIndex = winnerLaneIndex;
                    OwnerThreadId = ownerThreadId;
                    VirtualThreadId = virtualThreadId;
                    FaultAddress = faultAddress;
                    FaultIsWrite = faultIsWrite;
                    PC = pc;
                }

                public bool IsValid => WinnerStage != PipelineStage.None && WinnerLaneIndex < 8;
            }

            /// <summary>
            /// Resolve the authoritative stage-aware exception winner metadata from materialized stage lane state.
            /// Returns full identity (VT, owner thread, fault address, PC) for the oldest deterministic winner.
            /// </summary>
            public static bool TryResolveStageAwareExceptionWinnerMetadata(
                in WriteBackStage writeBackStage,
                in MemoryStage memoryStage,
                in ExecuteStage executeStage,
                out StageAwareExceptionWinnerMetadata metadata)
            {
                if (!TryResolveStageAwareExceptionWinner(
                    writeBackStage,
                    memoryStage,
                    executeStage,
                    out PipelineStage winnerStage,
                    out byte winnerLaneIndex))
                {
                    metadata = default;
                    return false;
                }

                metadata = winnerStage switch
                {
                    PipelineStage.WriteBack => BuildWriteBackWinnerMetadata(writeBackStage, winnerLaneIndex),
                    PipelineStage.Memory => BuildMemoryWinnerMetadata(memoryStage, winnerLaneIndex),
                    PipelineStage.Execute => BuildExecuteWinnerMetadata(executeStage, winnerLaneIndex),
                    _ => default
                };

                return metadata.IsValid;
            }

            /// <summary>
            /// Determine whether the exception winner from an older stage (WB or MEM) should suppress
            /// all younger occupied live-subset work in later stages. Returns true when the winner is
            /// in WB or MEM and at least one younger stage still has occupied lanes inside the current
            /// precise lane0..5 exception subset.
            /// </summary>
            public static bool ShouldSuppressYoungerWorkForExceptionWinner(
                PipelineStage winnerStage,
                in WriteBackStage writeBackStage,
                in MemoryStage memoryStage,
                in ExecuteStage executeStage)
            {
                if (winnerStage == PipelineStage.WriteBack)
                {
                    return GetOccupiedLivePreciseExceptionMemoryLaneMask(memoryStage) != 0 ||
                           GetOccupiedLivePreciseExceptionExecuteLaneMask(executeStage) != 0;
                }

                if (winnerStage == PipelineStage.Memory)
                {
                    return GetOccupiedLivePreciseExceptionExecuteLaneMask(executeStage) != 0;
                }

                return false;
            }

            /// <summary>
            /// Determine whether the specified stage can deliver an older-stage fault.
            /// WB and MEM fault carriers are deliverable when the winner stage matches.
            /// </summary>
            public static bool CanDeliverOlderStageFault(PipelineStage winnerStage)
            {
                return winnerStage == PipelineStage.WriteBack ||
                       winnerStage == PipelineStage.Memory;
            }

            /// <summary>
            /// Resolve the mask of younger-stage occupied lanes that are suppressed by the exception winner.
            /// When the winner is in WB, all occupied younger MEM and EX lanes inside the live precise
            /// lane0..5 subset are suppressed. When the winner is in MEM, all occupied younger EX lanes
            /// inside that same subset are suppressed.
            /// Returns 0 when the winner is in EX or when no younger lanes exist.
            /// </summary>
            public static byte ResolveExceptionWinnerSuppressedLaneMask(
                PipelineStage winnerStage,
                in MemoryStage memoryStage,
                in ExecuteStage executeStage)
            {
                byte suppressedMask = 0;

                if (winnerStage == PipelineStage.WriteBack)
                {
                    suppressedMask |= GetOccupiedLivePreciseExceptionMemoryLaneMask(memoryStage);
                    suppressedMask |= GetOccupiedLivePreciseExceptionExecuteLaneMask(executeStage);
                }
                else if (winnerStage == PipelineStage.Memory)
                {
                    suppressedMask |= GetOccupiedLivePreciseExceptionExecuteLaneMask(executeStage);
                }

                return suppressedMask;
            }

            /// <summary>
            /// Resolve the exception delivery decision for the current retire window.
            /// When faulted WB lanes remain after retire, the stage-aware winner policy determines
            /// whether a precise fault should be delivered or younger work silently squashed.
            /// Returns true when a delivery decision is produced (fault delivery or speculative squash).
            /// </summary>
            public static bool TryResolveExceptionDeliveryDecisionForRetireWindow(
                in WriteBackStage writeBackStage,
                in MemoryStage memoryStage,
                in ExecuteStage executeStage,
                out PipelineStage winnerStage,
                out byte winnerLaneIndex,
                out bool shouldSuppressYoungerWork)
            {
                byte wbFaultMask = ResolveWriteBackFaultMask(writeBackStage);
                byte memFaultMask = ResolveMemoryFaultMask(memoryStage);
                byte exFaultMask = ResolveExecuteFaultMask(executeStage);

                if (wbFaultMask == 0 && memFaultMask == 0 && exFaultMask == 0)
                {
                    winnerStage = PipelineStage.None;
                    winnerLaneIndex = byte.MaxValue;
                    shouldSuppressYoungerWork = false;
                    return false;
                }

                if (!TryResolveStageAwareExceptionWinner(
                        writeBackStage,
                        memoryStage,
                        executeStage,
                        out winnerStage,
                        out winnerLaneIndex))
                {
                    shouldSuppressYoungerWork = false;
                    return false;
                }

                shouldSuppressYoungerWork = ShouldSuppressYoungerWorkForExceptionWinner(
                    winnerStage, writeBackStage, memoryStage, executeStage);

                return true;
            }

            /// <summary>
            /// Resolve the authoritative exception winner identity for VT/thread consistency checking.
            /// Returns true when the winner's VirtualThreadId and OwnerThreadId are both valid (non-negative).
            /// </summary>
            public static bool TryResolveStageAwareExceptionWinnerIdentity(
                in WriteBackStage writeBackStage,
                in MemoryStage memoryStage,
                in ExecuteStage executeStage,
                out int winnerVirtualThreadId,
                out int winnerOwnerThreadId)
            {
                if (!TryResolveStageAwareExceptionWinnerMetadata(
                    writeBackStage,
                    memoryStage,
                    executeStage,
                    out StageAwareExceptionWinnerMetadata metadata))
                {
                    winnerVirtualThreadId = -1;
                    winnerOwnerThreadId = -1;
                    return false;
                }

                winnerVirtualThreadId = metadata.VirtualThreadId;
                winnerOwnerThreadId = metadata.OwnerThreadId;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static StageAwareExceptionWinnerMetadata BuildWriteBackWinnerMetadata(
                in WriteBackStage writeBackStage, byte laneIndex)
            {
                ScalarWriteBackLaneState lane = writeBackStage.GetLane(laneIndex);
                return new StageAwareExceptionWinnerMetadata(
                    PipelineStage.WriteBack, laneIndex,
                    lane.OwnerThreadId, lane.VirtualThreadId,
                    lane.FaultAddress, lane.FaultIsWrite, lane.PC);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static StageAwareExceptionWinnerMetadata BuildMemoryWinnerMetadata(
                in MemoryStage memoryStage, byte laneIndex)
            {
                ScalarMemoryLaneState lane = memoryStage.GetLane(laneIndex);
                return new StageAwareExceptionWinnerMetadata(
                    PipelineStage.Memory, laneIndex,
                    lane.OwnerThreadId, lane.VirtualThreadId,
                    lane.FaultAddress, lane.FaultIsWrite, lane.PC);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static StageAwareExceptionWinnerMetadata BuildExecuteWinnerMetadata(
                in ExecuteStage executeStage, byte laneIndex)
            {
                ScalarExecuteLaneState lane = executeStage.GetLane(laneIndex);
                return new StageAwareExceptionWinnerMetadata(
                    PipelineStage.Execute, laneIndex,
                    lane.OwnerThreadId, lane.VirtualThreadId,
                    lane.FaultAddress, lane.FaultIsWrite, lane.PC);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveOldestOrderedWriteBackFaultLane(
                in WriteBackStage writeBackStage,
                out byte laneIndex)
            {
                laneIndex = byte.MaxValue;
                byte faultMask = ResolveWriteBackFaultMask(writeBackStage);
                bool found = false;

                for (byte candidateLaneIndex = 0; candidateLaneIndex < 8; candidateLaneIndex++)
                {
                    if ((faultMask & (1 << candidateLaneIndex)) == 0)
                        continue;

                    if (!found ||
                        CompareWriteBackLaneOrder(
                            writeBackStage,
                            candidateLaneIndex,
                            laneIndex) < 0)
                    {
                        laneIndex = candidateLaneIndex;
                        found = true;
                    }
                }

                return found;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveOldestOrderedMemoryFaultLane(
                in MemoryStage memoryStage,
                out byte laneIndex)
            {
                return TryResolveOldestOrderedMemoryLaneIndex(
                    memoryStage,
                    ResolveMemoryFaultMask(memoryStage),
                    out laneIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveOldestOrderedExecuteFaultLane(
                in ExecuteStage executeStage,
                out byte laneIndex)
            {
                return TryResolveOldestOrderedExecuteLaneIndex(
                    executeStage,
                    ResolveExecuteFaultMask(executeStage),
                    out laneIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveOldestOrderedMemoryLaneIndex(
                in MemoryStage memoryStage,
                byte laneMask,
                out byte laneIndex)
            {
                laneIndex = byte.MaxValue;
                bool found = false;

                for (byte candidateLaneIndex = 0; candidateLaneIndex < 8; candidateLaneIndex++)
                {
                    if ((laneMask & (1 << candidateLaneIndex)) == 0)
                        continue;

                    if (!found ||
                        CompareMemoryLaneOrder(
                            memoryStage,
                            candidateLaneIndex,
                            laneIndex) < 0)
                    {
                        laneIndex = candidateLaneIndex;
                        found = true;
                    }
                }

                return found;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveOldestOrderedExecuteLaneIndex(
                in ExecuteStage executeStage,
                byte laneMask,
                out byte laneIndex)
            {
                laneIndex = byte.MaxValue;
                bool found = false;

                for (byte candidateLaneIndex = 0; candidateLaneIndex < 8; candidateLaneIndex++)
                {
                    if ((laneMask & (1 << candidateLaneIndex)) == 0)
                        continue;

                    if (!found ||
                        CompareExecuteLaneOrder(
                            executeStage,
                            candidateLaneIndex,
                            laneIndex) < 0)
                    {
                        laneIndex = candidateLaneIndex;
                        found = true;
                    }
                }

                return found;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CompareMemoryLaneOrder(
                in MemoryStage memoryStage,
                byte leftLaneIndex,
                byte rightLaneIndex)
            {
                if (leftLaneIndex == rightLaneIndex)
                    return 0;

                if (TryResolveMemoryLaneOrderKey(memoryStage, leftLaneIndex, out int leftOrderKey) &&
                    TryResolveMemoryLaneOrderKey(memoryStage, rightLaneIndex, out int rightOrderKey))
                {
                    int compareResult = leftOrderKey.CompareTo(rightOrderKey);
                    if (compareResult != 0)
                        return compareResult;
                }

                return leftLaneIndex.CompareTo(rightLaneIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveMemoryLaneOrderKey(
                in MemoryStage memoryStage,
                byte laneIndex,
                out int orderKey)
            {
                orderKey = laneIndex;
                if (!memoryStage.UsesExplicitPacketLanes)
                    return false;

                ScalarMemoryLaneState lane = memoryStage.GetLane(laneIndex);
                if (!lane.IsOccupied || lane.SlotIndex >= 8)
                    return false;

                orderKey = lane.SlotIndex;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CompareExecuteLaneOrder(
                in ExecuteStage executeStage,
                byte leftLaneIndex,
                byte rightLaneIndex)
            {
                if (leftLaneIndex == rightLaneIndex)
                    return 0;

                if (TryResolveExecuteLaneOrderKey(executeStage, leftLaneIndex, out int leftOrderKey) &&
                    TryResolveExecuteLaneOrderKey(executeStage, rightLaneIndex, out int rightOrderKey))
                {
                    int compareResult = leftOrderKey.CompareTo(rightOrderKey);
                    if (compareResult != 0)
                        return compareResult;
                }

                return leftLaneIndex.CompareTo(rightLaneIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveExecuteLaneOrderKey(
                in ExecuteStage executeStage,
                byte laneIndex,
                out int orderKey)
            {
                orderKey = laneIndex;
                if (!executeStage.UsesExplicitPacketLanes)
                    return false;

                ScalarExecuteLaneState lane = executeStage.GetLane(laneIndex);
                if (!lane.IsOccupied || lane.SlotIndex >= 8)
                    return false;

                orderKey = lane.SlotIndex;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetOccupiedScalarMemoryLaneMask(in MemoryStage memoryStage)
            {
                byte occupiedMask = 0;

                if (memoryStage.Lane0.IsOccupied)
                    occupiedMask |= 1 << 0;

                if (memoryStage.Lane1.IsOccupied)
                    occupiedMask |= 1 << 1;

                if (memoryStage.Lane2.IsOccupied)
                    occupiedMask |= 1 << 2;

                if (memoryStage.Lane3.IsOccupied)
                    occupiedMask |= 1 << 3;

                return occupiedMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetOccupiedLivePreciseExceptionExecuteLaneMask(in ExecuteStage executeStage)
            {
                byte occupiedMask = GetOccupiedScalarExecuteLaneMask(executeStage);

                if (executeStage.Lane4.IsOccupied)
                    occupiedMask |= 1 << 4;

                if (executeStage.Lane5.IsOccupied)
                    occupiedMask |= 1 << 5;

                return occupiedMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetOccupiedLivePreciseExceptionMemoryLaneMask(in MemoryStage memoryStage)
            {
                byte occupiedMask = GetOccupiedScalarMemoryLaneMask(memoryStage);

                if (memoryStage.Lane4.IsOccupied)
                    occupiedMask |= 1 << 4;

                if (memoryStage.Lane5.IsOccupied)
                    occupiedMask |= 1 << 5;

                return occupiedMask;
            }
        }
    }
}
