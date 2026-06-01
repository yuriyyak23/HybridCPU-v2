using System.Numerics;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldMaterializeIssuePacketLane(
                Core.BundleIssuePacket issuePacket,
                byte laneIndex,
                byte executableNonScalarPhysicalLaneMask)
            {
                if (laneIndex < 4)
                    return issuePacket.GetPhysicalLane(laneIndex).IsOccupied;

                return (executableNonScalarPhysicalLaneMask & (1 << laneIndex)) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ushort ResolvePrimaryWriteRegister(Core.MicroOp? microOp)
            {
                var writeRegisters = microOp?.WriteRegisters;
                if (writeRegisters == null || writeRegisters.Count == 0)
                    return 0;

                return unchecked((ushort)writeRegisters[0]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.IssuePacketLane ResolveMaterializedIssuePacketLane(
                Core.BundleIssuePacket issuePacket,
                byte laneIndex,
                byte executableNonScalarPhysicalLaneMask)
            {
                if (!ShouldMaterializeIssuePacketLane(
                    issuePacket,
                    laneIndex,
                    executableNonScalarPhysicalLaneMask))
                {
                    return Core.IssuePacketLane.CreateEmpty(laneIndex);
                }

                Core.IssuePacketLane issueLane = issuePacket.GetPhysicalLane(laneIndex);
                issueLane = ApplyIssueLaneExecutionSurfaceContract(issueLane, issuePacket.PC);
                return CanVirtualThreadIssueInForeground(issueLane.OwnerThreadId)
                    ? issueLane
                    : Core.IssuePacketLane.CreateEmpty(laneIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void MaterializeExecuteStageLaneState()
            {
                pipeEX.Clear();

                Core.BundleIssuePacket issuePacket = pipeIDAdmissionHandoff.IssuePacket;
                byte executableNonScalarPhysicalLaneMask =
                    ResolveExecutableNonScalarPhysicalLaneMask(
                        issuePacket,
                        pipeIDAdmissionHandoff.DependencySummary);
                pipeEX.Valid = pipeID.Valid;
                pipeEX.PreparedScalarMask = issuePacket.PreparedScalarMask;
                pipeEX.RefinedPreparedScalarMask = issuePacket.RefinedPreparedScalarMask;
                pipeEX.RetainsReferenceSequentialPath = issuePacket.RetainsReferenceSequentialPath;
                pipeEX.SelectedNonScalarSlotMask = ResolveExecutableNonScalarSlotMask(
                    issuePacket,
                    executableNonScalarPhysicalLaneMask);
                pipeEX.BlockedScalarCandidateMask = issuePacket.FallbackInfo.BlockedScalarCandidateMask;
                pipeEX.AdmissionExecutionMode = pipeIDAdmissionDecisionDraft.ExecutionMode;

                if (pipeIDAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource)
                {
                    pipeEX.UsesExplicitPacketLanes = true;
                    int preparedScalarLaneCount = issuePacket.PreparedScalarLaneCount;
                    int preparedPhysicalLaneCount = issuePacket.PreparedPhysicalLaneCount;
                    pipeEX.MaterializedScalarLaneCount = preparedScalarLaneCount;
                    pipeEX.MaterializedPhysicalLaneCount = preparedScalarLaneCount +
                        BitOperations.PopCount((uint)executableNonScalarPhysicalLaneMask);
                    pipeEX.Lane0 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 0, executableNonScalarPhysicalLaneMask));
                    pipeEX.Lane1 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 1, executableNonScalarPhysicalLaneMask));
                    pipeEX.Lane2 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 2, executableNonScalarPhysicalLaneMask));
                    pipeEX.Lane3 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 3, executableNonScalarPhysicalLaneMask));
                    pipeEX.Lane4 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 4, executableNonScalarPhysicalLaneMask));
                    pipeEX.Lane5 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 5, executableNonScalarPhysicalLaneMask));
                    pipeEX.Lane6 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 6, executableNonScalarPhysicalLaneMask));
                    pipeEX.Lane7 = CreateExecuteLaneState(issuePacket.PC, ResolveMaterializedIssuePacketLane(issuePacket, 7, executableNonScalarPhysicalLaneMask));
                    pipeEX.ActiveLaneIndex = ResolveActiveMaterializedIssueLaneIndex(issuePacket, pipeID.SlotIndex);

                    pipeCtrl.IssuePacketPreparedLaneCountSum += (ulong)preparedScalarLaneCount;
                    pipeCtrl.IssuePacketPreparedPhysicalLaneCountSum += (ulong)preparedPhysicalLaneCount;

                    // Stage 6 Phase A+B: enrich each occupied lane with MicroOp, classification,
                    // per-lane GRLB resource acquisition, and per-lane MSHR scoreboard registration.
                    EnrichClusterPreparedLanes(issuePacket, executableNonScalarPhysicalLaneMask);

                    int materializedScalarLaneCount = CountOccupiedScalarExecuteLanes(pipeEX);
                    int materializedPhysicalLaneCount = CountOccupiedPhysicalExecuteLanes(pipeEX);
                    pipeEX.MaterializedScalarLaneCount = materializedScalarLaneCount;
                    pipeEX.MaterializedPhysicalLaneCount = materializedPhysicalLaneCount;
                    pipeCtrl.IssuePacketMaterializedLaneCountSum += (ulong)materializedScalarLaneCount;
                    pipeCtrl.IssuePacketMaterializedPhysicalLaneCountSum += (ulong)materializedPhysicalLaneCount;
                    if (materializedPhysicalLaneCount < preparedPhysicalLaneCount)
                    {
                        pipeCtrl.IssuePacketWidthDropCount++;
                    }

                    return;
                }

                pipeEX.UsesExplicitPacketLanes = false;
                pipeEX.MaterializedScalarLaneCount = pipeID.Valid ? 1 : 0;
                pipeEX.MaterializedPhysicalLaneCount = pipeID.Valid ? 1 : 0;
                pipeEX.ActiveLaneIndex = 0;
                pipeEX.Lane0 = CreateLegacyExecuteLaneState(pipeID);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void MaterializeMemoryStageLaneState()
            {
                pipeMEM.Clear();
                pipeMEM.Valid = pipeEX.Valid;
                pipeMEM.ActiveLaneIndex = pipeEX.ActiveLaneIndex;
                pipeMEM.PreparedScalarMask = pipeEX.PreparedScalarMask;
                pipeMEM.RefinedPreparedScalarMask = pipeEX.RefinedPreparedScalarMask;
                pipeMEM.MaterializedScalarLaneCount = pipeEX.MaterializedScalarLaneCount;
                pipeMEM.MaterializedPhysicalLaneCount = pipeEX.MaterializedPhysicalLaneCount;
                pipeMEM.UsesExplicitPacketLanes = pipeEX.UsesExplicitPacketLanes;
                pipeMEM.RetainsReferenceSequentialPath = pipeEX.RetainsReferenceSequentialPath;
                pipeMEM.SelectedNonScalarSlotMask = pipeEX.SelectedNonScalarSlotMask;
                pipeMEM.BlockedScalarCandidateMask = pipeEX.BlockedScalarCandidateMask;
                pipeMEM.AdmissionExecutionMode = pipeEX.AdmissionExecutionMode;
                pipeMEM.Lane0 = CreateMemoryLaneState(pipeEX.Lane0);
                pipeMEM.Lane1 = CreateMemoryLaneState(pipeEX.Lane1);
                pipeMEM.Lane2 = CreateMemoryLaneState(pipeEX.Lane2);
                pipeMEM.Lane3 = CreateMemoryLaneState(pipeEX.Lane3);
                pipeMEM.Lane4 = CreateMemoryLaneState(pipeEX.Lane4);
                pipeMEM.Lane5 = CreateMemoryLaneState(pipeEX.Lane5);
                pipeMEM.Lane6 = CreateMemoryLaneState(pipeEX.Lane6);
                pipeMEM.Lane7 = CreateMemoryLaneState(pipeEX.Lane7);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LatchExecuteToMemoryTransferState()
            {
                MaterializeMemoryStageLaneState();

                // Preserve EX-owned ownership, scoreboarding, and resource facts on the MEM latch.
                pipeMEM.DomainTag = pipeEX.DomainTag;
                pipeMEM.MshrScoreboardSlot = pipeEX.MshrScoreboardSlot;
                pipeMEM.MshrVirtualThreadId = pipeEX.MshrVirtualThreadId;
                pipeMEM.ResourceMask = pipeEX.ResourceMask;
                pipeMEM.ResourceToken = pipeEX.ResourceToken;
                pipeMEM.OwnerThreadId = pipeEX.OwnerThreadId;
                pipeMEM.VirtualThreadId = pipeEX.VirtualThreadId;
                pipeMEM.OwnerContextId = pipeEX.OwnerContextId;
                pipeMEM.WasFspInjected = pipeEX.WasFspInjected;
                pipeMEM.OriginalThreadId = pipeEX.OriginalThreadId;
                pipeMEM.AdmissionExecutionMode = pipeEX.AdmissionExecutionMode;
                pipeMEM.Valid = true;
                pipeEX.Valid = false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LatchMemoryToWriteBackTransferState()
            {
                MaterializeWriteBackStageLaneState();

                pipeWB.DomainTag = pipeMEM.DomainTag;
                pipeWB.MshrScoreboardSlot = pipeMEM.MshrScoreboardSlot;
                pipeWB.MshrVirtualThreadId = pipeMEM.MshrVirtualThreadId;
                pipeWB.ResourceMask = pipeMEM.ResourceMask;
                pipeWB.ResourceToken = pipeMEM.ResourceToken;
                pipeWB.OwnerThreadId = pipeMEM.OwnerThreadId;
                pipeWB.VirtualThreadId = pipeMEM.VirtualThreadId;
                pipeWB.OwnerContextId = pipeMEM.OwnerContextId;
                pipeWB.WasFspInjected = pipeMEM.WasFspInjected;
                pipeWB.OriginalThreadId = pipeMEM.OriginalThreadId;
                pipeWB.AdmissionExecutionMode = pipeMEM.AdmissionExecutionMode;
                pipeWB.Valid = true;
                pipeMEM.Valid = false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryApplyWriteBackStageDomainSquash()
            {
                if (pipeWB.DomainTag == 0 || CsrMemDomainCert == 0)
                    return false;

                if ((pipeWB.DomainTag & CsrMemDomainCert) != 0)
                    return false;

                pipeWB.WritesRegister = false;
                pipeWB.ResultValue = 0;
                pipeCtrl.DomainSquashCount++;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void MaterializeWriteBackStageLaneState()
            {
                pipeWB.Clear();
                pipeWB.Valid = pipeMEM.Valid;
                pipeWB.ActiveLaneIndex = pipeMEM.ActiveLaneIndex;
                pipeWB.PreparedScalarMask = pipeMEM.PreparedScalarMask;
                pipeWB.RefinedPreparedScalarMask = pipeMEM.RefinedPreparedScalarMask;
                pipeWB.MaterializedScalarLaneCount = pipeMEM.MaterializedScalarLaneCount;
                pipeWB.MaterializedPhysicalLaneCount = pipeMEM.MaterializedPhysicalLaneCount;
                pipeWB.UsesExplicitPacketLanes = pipeMEM.UsesExplicitPacketLanes;
                pipeWB.RetainsReferenceSequentialPath = pipeMEM.RetainsReferenceSequentialPath;
                pipeWB.SelectedNonScalarSlotMask = pipeMEM.SelectedNonScalarSlotMask;
                pipeWB.BlockedScalarCandidateMask = pipeMEM.BlockedScalarCandidateMask;
                pipeWB.AdmissionExecutionMode = pipeMEM.AdmissionExecutionMode;
                pipeWB.Lane0 = CreateWriteBackLaneState(pipeMEM.Lane0);
                pipeWB.Lane1 = CreateWriteBackLaneState(pipeMEM.Lane1);
                pipeWB.Lane2 = CreateWriteBackLaneState(pipeMEM.Lane2);
                pipeWB.Lane3 = CreateWriteBackLaneState(pipeMEM.Lane3);
                pipeWB.Lane4 = CreateWriteBackLaneState(pipeMEM.Lane4);
                pipeWB.Lane5 = CreateWriteBackLaneState(pipeMEM.Lane5);
                pipeWB.Lane6 = CreateWriteBackLaneState(pipeMEM.Lane6);
                pipeWB.Lane7 = CreateWriteBackLaneState(pipeMEM.Lane7);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Processor.CPU_Core.ScalarExecuteLaneState CreateExecuteLaneState(
                ulong pc,
                Core.IssuePacketLane issueLane)
            {
                Processor.CPU_Core.ScalarExecuteLaneState lane = new();
                lane.Clear(issueLane.LaneIndex);

                if (!issueLane.IsOccupied)
                    return lane;

                lane.IsOccupied = true;
                lane.PC = pc;
                lane.SlotIndex = issueLane.SlotIndex;
                lane.OpCode = issueLane.OpCode;
                lane.MicroOp = issueLane.MicroOp;
                lane.IsMemoryOp = issueLane.MicroOp?.IsMemoryOp ?? false;
                lane.WritesRegister = issueLane.MicroOp?.WritesRegister ?? false;
                lane.DestRegID = issueLane.MicroOp?.DestRegID ?? 0;
                lane.DomainTag = issueLane.MicroOp?.Placement.DomainTag ?? 0;
                lane.OwnerThreadId = issueLane.OwnerThreadId;
                lane.VirtualThreadId = issueLane.VirtualThreadId;
                lane.OwnerContextId = issueLane.MicroOp?.OwnerContextId ?? 0;
                lane.WasFspInjected = issueLane.MicroOp?.IsFspInjected ?? false;
                lane.OriginalThreadId = issueLane.OwnerThreadId;
                return lane;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Processor.CPU_Core.ScalarExecuteLaneState CreateLegacyExecuteLaneState(DecodeStage decodeStage)
            {
                Processor.CPU_Core.ScalarExecuteLaneState lane = new();
                lane.Clear(0);

                if (!decodeStage.Valid)
                    return lane;

                lane.IsOccupied = true;
                lane.PC = decodeStage.PC;
                lane.SlotIndex = decodeStage.SlotIndex;
                lane.OpCode = decodeStage.OpCode;
                lane.IsMemoryOp = decodeStage.IsMemoryOp;
                lane.IsVectorOp = decodeStage.IsVectorOp;
                lane.WritesRegister = decodeStage.WritesRegister;
                lane.DestRegID = ResolvePrimaryWriteRegister(decodeStage.MicroOp);
                lane.MicroOp = decodeStage.MicroOp;
                lane.OwnerThreadId = decodeStage.MicroOp?.OwnerThreadId ?? 0;
                lane.VirtualThreadId = decodeStage.MicroOp?.VirtualThreadId ?? 0;
                lane.OwnerContextId = decodeStage.MicroOp?.OwnerContextId ?? 0;
                lane.WasFspInjected = decodeStage.MicroOp?.IsFspInjected ?? false;
                lane.OriginalThreadId = decodeStage.MicroOp?.OwnerThreadId ?? 0;
                return lane;
            }

            /// <summary>
            /// Stage 6 Phase A+B: enrich each occupied lane in the cluster-prepared packet
            /// with MicroOp reference, classification flags, per-lane GRLB resource acquisition,
            /// per-lane MSHR scoreboard registration, and DomainTag propagation.
            /// The MicroOp is carried by the issue packet so decode may clear consumed bundle slots
            /// without starving EX lane materialization.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void EnrichClusterPreparedLanes(
                Core.BundleIssuePacket issuePacket,
                byte executableNonScalarPhysicalLaneMask)
            {
                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                    if (!lane.IsOccupied)
                        continue;

                    Core.IssuePacketLane issueLane = issuePacket.GetPhysicalLane(laneIndex);
                    Core.MicroOp laneOp = issueLane.MicroOp;

                    if (laneOp != null)
                    {
                        lane.MicroOp = laneOp;
                        lane.IsMemoryOp = laneOp.IsMemoryOp;
                        lane.WritesRegister = laneOp.WritesRegister;
                        lane.DestRegID = laneOp.DestRegID;
                        lane.DomainTag = laneOp.Placement.DomainTag;
                        lane.WasFspInjected = laneOp.IsFspInjected;

                        var laneResourceMask = laneOp.ResourceMask;
                        if (laneResourceMask.IsNonZero)
                        {
                            AcquireResourcesWithToken(laneResourceMask, out ulong laneResourceToken);
                            lane.ResourceMask = laneResourceMask;
                            lane.ResourceToken = laneResourceToken;
                        }

                        lane.MshrScoreboardSlot = -1;
                        lane.MshrVirtualThreadId = laneOp.VirtualThreadId;
                        if (laneOp is Core.LoadStoreMicroOp lsOp && _fspScheduler != null)
                        {
                            int bankId = lsOp.MemoryBankId;
                            int vtId = lsOp.VirtualThreadId;
                            var entryType = lsOp is Core.LoadMicroOp
                                ? Core.ScoreboardEntryType.OutstandingLoad
                                : Core.ScoreboardEntryType.OutstandingStore;
                            int slot = _fspScheduler.SetSmtScoreboardPendingTyped(
                                bankId, vtId, (long)pipeCtrl.CycleCount, entryType, bankId);
                            lane.MshrScoreboardSlot = slot;
                        }

                        if (laneOp is Core.LoadMicroOp loadOp)
                        {
                            lane.IsMemoryOp = true;
                            lane.IsLoad = true;
                            lane.MemoryAccessSize = loadOp.Size;
                            lane.MemoryAddress = loadOp.Address;
                        }
                        else if (laneOp is Core.StoreMicroOp storeOp &&
                            (executableNonScalarPhysicalLaneMask & (1 << laneIndex)) != 0)
                        {
                            lane.IsMemoryOp = true;
                            lane.IsLoad = false;
                            lane.MemoryAccessSize = storeOp.Size;
                            lane.MemoryAddress = storeOp.Address;
                        }
                    }

                    pipeEX.SetLane(laneIndex, lane);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Processor.CPU_Core.ScalarMemoryLaneState CreateMemoryLaneState(ScalarExecuteLaneState executeLane)
            {
                Processor.CPU_Core.ScalarMemoryLaneState lane = new();
                lane.Clear(executeLane.LaneIndex);

                if (!executeLane.IsOccupied)
                    return lane;

                lane.IsOccupied = true;
                lane.PC = executeLane.PC;
                lane.SlotIndex = executeLane.SlotIndex;
                lane.OpCode = executeLane.OpCode;
                lane.ResultValue = executeLane.ResultValue;
                lane.ResultReady = executeLane.ResultReady;
                lane.IsMemoryOp = executeLane.IsMemoryOp;
                lane.MemoryAddress = executeLane.MemoryAddress;
                lane.MemoryData = executeLane.MemoryData;
                lane.IsLoad = executeLane.IsLoad;
                lane.MemoryAccessSize = executeLane.MemoryAccessSize;
                lane.IsVectorOp = executeLane.IsVectorOp;
                lane.VectorComplete = executeLane.VectorComplete;
                lane.WritesRegister = executeLane.WritesRegister;
                lane.DestRegID = executeLane.DestRegID;
                lane.MicroOp = executeLane.MicroOp;
                lane.GeneratedEvent = executeLane.GeneratedEvent;
                lane.GeneratedCsrEffect = executeLane.GeneratedCsrEffect;
                lane.GeneratedAtomicEffect = executeLane.GeneratedAtomicEffect;
                lane.GeneratedVmxEffect = executeLane.GeneratedVmxEffect;
                lane.GeneratedRetireRecordCount = executeLane.GeneratedRetireRecordCount;
                lane.GeneratedRetireRecord0 = executeLane.GeneratedRetireRecord0;
                lane.GeneratedRetireRecord1 = executeLane.GeneratedRetireRecord1;
                lane.DomainTag = executeLane.DomainTag;
                lane.MshrScoreboardSlot = executeLane.MshrScoreboardSlot;
                lane.MshrVirtualThreadId = executeLane.MshrVirtualThreadId;
                lane.ResourceMask = executeLane.ResourceMask;
                lane.ResourceToken = executeLane.ResourceToken;
                lane.OwnerThreadId = executeLane.OwnerThreadId;
                lane.VirtualThreadId = executeLane.VirtualThreadId;
                // Propagate architectural privilege-domain id through EX->MEM.
                lane.OwnerContextId = executeLane.OwnerContextId;
                lane.WasFspInjected = executeLane.WasFspInjected;
                lane.OriginalThreadId = executeLane.OriginalThreadId;
                lane.HasFault = executeLane.HasFault;
                lane.FaultAddress = executeLane.FaultAddress;
                lane.FaultIsWrite = executeLane.FaultIsWrite;

                if (executeLane.LaneIndex is 4 or 5 &&
                    executeLane.IsMemoryOp &&
                    !executeLane.GeneratedAtomicEffect.HasValue)
                {
                    // The live widened LSU load/store subset claims readiness from MEM completion,
                    // not from EX address preparation.
                    lane.ResultReady = false;
                }

                return lane;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Processor.CPU_Core.ScalarWriteBackLaneState CreateWriteBackLaneState(ScalarMemoryLaneState memoryLane)
            {
                Processor.CPU_Core.ScalarWriteBackLaneState lane = new();
                lane.Clear(memoryLane.LaneIndex);

                if (!memoryLane.IsOccupied)
                    return lane;

                lane.IsOccupied = true;
                lane.PC = memoryLane.PC;
                lane.SlotIndex = memoryLane.SlotIndex;
                lane.OpCode = memoryLane.OpCode;
                lane.ResultValue = memoryLane.ResultValue;
                lane.IsMemoryOp = memoryLane.IsMemoryOp;
                lane.MemoryAddress = memoryLane.MemoryAddress;
                lane.MemoryData = memoryLane.MemoryData;
                lane.IsLoad = memoryLane.IsLoad;
                lane.MemoryAccessSize = memoryLane.MemoryAccessSize;
                lane.WritesRegister = memoryLane.WritesRegister;
                lane.DestRegID = memoryLane.DestRegID;
                lane.MicroOp = memoryLane.MicroOp;
                lane.GeneratedEvent = memoryLane.GeneratedEvent;
                lane.GeneratedCsrEffect = memoryLane.GeneratedCsrEffect;
                lane.GeneratedAtomicEffect = memoryLane.GeneratedAtomicEffect;
                lane.GeneratedVmxEffect = memoryLane.GeneratedVmxEffect;
                lane.GeneratedRetireRecordCount = memoryLane.GeneratedRetireRecordCount;
                lane.GeneratedRetireRecord0 = memoryLane.GeneratedRetireRecord0;
                lane.GeneratedRetireRecord1 = memoryLane.GeneratedRetireRecord1;
                lane.DomainTag = memoryLane.DomainTag;
                lane.MshrScoreboardSlot = memoryLane.MshrScoreboardSlot;
                lane.MshrVirtualThreadId = memoryLane.MshrVirtualThreadId;
                lane.ResourceMask = memoryLane.ResourceMask;
                lane.ResourceToken = memoryLane.ResourceToken;
                lane.OwnerThreadId = memoryLane.OwnerThreadId;
                lane.VirtualThreadId = memoryLane.VirtualThreadId;
                // Propagate architectural privilege-domain id through MEM->WB.
                lane.OwnerContextId = memoryLane.OwnerContextId;
                lane.WasFspInjected = memoryLane.WasFspInjected;
                lane.OriginalThreadId = memoryLane.OriginalThreadId;
                lane.HasFault = memoryLane.HasFault;
                lane.FaultAddress = memoryLane.FaultAddress;
                lane.FaultIsWrite = memoryLane.FaultIsWrite;
                lane.DefersStoreCommitToWriteBack = memoryLane.DefersStoreCommitToWriteBack;
                return lane;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseScalarLaneBookkeeping(ScalarExecuteLaneState lane)
            {
                if (!lane.IsOccupied)
                    return;

                if (lane.ResourceToken != 0 && lane.ResourceMask.IsNonZero)
                {
                    ReleaseResourcesWithToken(lane.ResourceMask, lane.ResourceToken);
                }

                if (lane.MshrScoreboardSlot >= 0 && _fspScheduler != null)
                {
                    _fspScheduler.ClearSmtScoreboardEntry(lane.MshrVirtualThreadId, lane.MshrScoreboardSlot);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseScalarLaneBookkeeping(ScalarMemoryLaneState lane)
            {
                if (!lane.IsOccupied)
                    return;

                if (lane.ResourceToken != 0 && lane.ResourceMask.IsNonZero)
                {
                    ReleaseResourcesWithToken(lane.ResourceMask, lane.ResourceToken);
                }

                if (lane.MshrScoreboardSlot >= 0 && _fspScheduler != null)
                {
                    _fspScheduler.ClearSmtScoreboardEntry(lane.MshrVirtualThreadId, lane.MshrScoreboardSlot);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseScalarLaneBookkeeping(ScalarWriteBackLaneState lane)
            {
                if (!lane.IsOccupied)
                    return;

                if (lane.ResourceToken != 0 && lane.ResourceMask.IsNonZero)
                {
                    ReleaseResourcesWithToken(lane.ResourceMask, lane.ResourceToken);
                }

                if (lane.MshrScoreboardSlot >= 0 && _fspScheduler != null)
                {
                    _fspScheduler.ClearSmtScoreboardEntry(lane.MshrVirtualThreadId, lane.MshrScoreboardSlot);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseExecuteStageLaneAwareBookkeeping()
            {
                ReleaseScalarLaneBookkeeping(pipeEX.Lane0);
                ReleaseScalarLaneBookkeeping(pipeEX.Lane1);
                ReleaseScalarLaneBookkeeping(pipeEX.Lane2);
                ReleaseScalarLaneBookkeeping(pipeEX.Lane3);
                ReleaseScalarLaneBookkeeping(pipeEX.Lane4);
                ReleaseScalarLaneBookkeeping(pipeEX.Lane5);
                ReleaseScalarLaneBookkeeping(pipeEX.Lane6);
                ReleaseScalarLaneBookkeeping(pipeEX.Lane7);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseMemoryStageLaneAwareBookkeeping()
            {
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane0);
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane1);
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane2);
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane3);
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane4);
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane5);
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane6);
                ReleaseScalarLaneBookkeeping(pipeMEM.Lane7);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseWriteBackStageLaneAwareBookkeeping()
            {
                ReleaseScalarLaneBookkeeping(pipeWB.Lane0);
                ReleaseScalarLaneBookkeeping(pipeWB.Lane1);
                ReleaseScalarLaneBookkeeping(pipeWB.Lane2);
                ReleaseScalarLaneBookkeeping(pipeWB.Lane3);
                ReleaseScalarLaneBookkeeping(pipeWB.Lane4);
                ReleaseScalarLaneBookkeeping(pipeWB.Lane5);
                ReleaseScalarLaneBookkeeping(pipeWB.Lane6);
                ReleaseScalarLaneBookkeeping(pipeWB.Lane7);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseAllInFlightLaneAwareBookkeeping()
            {
                ReleaseExecuteStageLaneAwareBookkeeping();
                ReleaseMemoryStageLaneAwareBookkeeping();
                ReleaseWriteBackStageLaneAwareBookkeeping();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveActiveMaterializedIssueLaneIndex(Core.BundleIssuePacket issuePacket, byte slotIndex)
            {
                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (lane.IsOccupied && lane.SlotIndex == slotIndex)
                        return laneIndex;
                }

                byte oldestLaneIndex = byte.MaxValue;
                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (!lane.IsOccupied)
                        continue;

                    if (oldestLaneIndex == byte.MaxValue ||
                        CompareIssuePacketLaneOrder(
                            issuePacket,
                            laneIndex,
                            oldestLaneIndex) < 0)
                    {
                        oldestLaneIndex = laneIndex;
                    }
                }

                return oldestLaneIndex < 8
                    ? oldestLaneIndex
                    : (byte)0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CompareIssuePacketLaneOrder(
                Core.BundleIssuePacket issuePacket,
                byte leftLaneIndex,
                byte rightLaneIndex)
            {
                if (leftLaneIndex == rightLaneIndex)
                    return 0;

                Core.IssuePacketLane leftLane = issuePacket.GetPhysicalLane(leftLaneIndex);
                Core.IssuePacketLane rightLane = issuePacket.GetPhysicalLane(rightLaneIndex);
                if (leftLane.IsOccupied &&
                    rightLane.IsOccupied &&
                    leftLane.SlotIndex < 8 &&
                    rightLane.SlotIndex < 8)
                {
                    int compareResult = leftLane.SlotIndex.CompareTo(rightLane.SlotIndex);
                    if (compareResult != 0)
                        return compareResult;
                }

                return leftLaneIndex.CompareTo(rightLaneIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CountOccupiedPhysicalExecuteLanes(in ExecuteStage executeStage)
            {
                int count = 0;
                if (executeStage.Lane0.IsOccupied) count++;
                if (executeStage.Lane1.IsOccupied) count++;
                if (executeStage.Lane2.IsOccupied) count++;
                if (executeStage.Lane3.IsOccupied) count++;
                if (executeStage.Lane4.IsOccupied) count++;
                if (executeStage.Lane5.IsOccupied) count++;
                if (executeStage.Lane6.IsOccupied) count++;
                if (executeStage.Lane7.IsOccupied) count++;
                return count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CountOccupiedPhysicalWriteBackLanes(in WriteBackStage writeBackStage)
            {
                int count = 0;
                if (writeBackStage.Lane0.IsOccupied) count++;
                if (writeBackStage.Lane1.IsOccupied) count++;
                if (writeBackStage.Lane2.IsOccupied) count++;
                if (writeBackStage.Lane3.IsOccupied) count++;
                if (writeBackStage.Lane4.IsOccupied) count++;
                if (writeBackStage.Lane5.IsOccupied) count++;
                if (writeBackStage.Lane6.IsOccupied) count++;
                if (writeBackStage.Lane7.IsOccupied) count++;
                return count;
            }
        }
    }
}
