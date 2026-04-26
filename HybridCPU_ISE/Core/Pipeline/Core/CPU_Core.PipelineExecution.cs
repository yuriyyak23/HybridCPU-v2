
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Stage 6 Phase B: execute all occupied explicit lanes in the current EX packet.
            /// Lanes execute in ascending lane-index order within a single cycle.
            /// On PageFaultException from any lane: MarkActiveExecuteLanePageFault + DeliverStageAwareExecutePageFault.
            /// On non-fault MicroOp failure for a lane: mark that lane's ResultReady = false; other lanes proceed.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ExecuteExplicitPacketLanes()
            {
                int executedPhysicalLaneCount = 0;
                int executedScalarLaneCount = 0;
                byte originalActiveLaneIndex = pipeEX.ActiveLaneIndex;
                byte completedLaneMask = 0;
                ulong completedContourPc = 0;

                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                    if (!lane.IsOccupied || lane.MicroOp == null)
                        continue;

                    pipeEX.ActiveLaneIndex = laneIndex;

                    try
                    {
                        if (TryExecuteExplicitPacketAssistLane(
                            laneIndex,
                            ref lane,
                            ref executedPhysicalLaneCount))
                        {
                            if (lane.ResultReady)
                            {
                                completedLaneMask |= (byte)(1 << laneIndex);
                                completedContourPc = lane.PC;
                            }
                            continue;
                        }

                        if (TryExecuteExplicitPacketLane7Branch(
                            laneIndex,
                            ref lane,
                            ref executedPhysicalLaneCount))
                        {
                            if (lane.ResultReady)
                            {
                                completedLaneMask |= (byte)(1 << laneIndex);
                                completedContourPc = lane.PC;
                            }
                            continue;
                        }

                        if (TryExecuteExplicitPacketGenericMicroOpLane(
                            laneIndex,
                            ref lane,
                            ref executedPhysicalLaneCount,
                            ref executedScalarLaneCount))
                        {
                            if (lane.ResultReady)
                            {
                                completedLaneMask |= (byte)(1 << laneIndex);
                                completedContourPc = lane.PC;
                            }
                            continue;
                        }

                        if (TryPrepareExplicitPacketExecuteMemoryCarrierLane(
                            laneIndex,
                            ref lane,
                            ref executedPhysicalLaneCount))
                        {
                            completedLaneMask |= (byte)(1 << laneIndex);
                            completedContourPc = lane.PC;
                            continue;
                        }

                        lane.ResultReady = false;
                        pipeEX.SetLane(laneIndex, lane);
                    }
                    catch (Core.PageFaultException pageFaultException)
                    {
                        RethrowExplicitPacketExecutePageFault(pageFaultException);
                    }
                    catch (Core.Memory.MemoryAlignmentException memoryAlignmentException)
                    {
                        RethrowExplicitPacketExecuteAlignmentFault(
                            memoryAlignmentException,
                            lane.MicroOp);
                    }
                    catch
                    {
                        // Non-fault execution failure: mark lane as not ready, other lanes proceed
                        FailCloseExplicitPacketLaneAfterNonFaultExecutionException(ref lane);
                    }
                }

                ApplyExplicitPacketExecuteEpilogueAccounting(
                    executedPhysicalLaneCount,
                    executedScalarLaneCount);
                PublishExecuteCompletionContourCertificate(
                    Core.PipelineContourOwner.ExplicitPacketExecution,
                    Core.PipelineContourVisibilityStage.Execute,
                    completedContourPc,
                    completedLaneMask);

                if (TryConsumeEmptyExplicitPacketAfterExecution())
                {
                    return;
                }

                PublishExplicitPacketExecuteForwarding();

                CompleteExplicitPacketExecuteDispatch(originalActiveLaneIndex);
            }

            /// <summary>
            /// Stage 3: Execute (EX)
            /// - Perform ALU operations
            /// - Calculate memory addresses
            /// - Execute vector operations (may take multiple cycles)
            /// - Check for data forwarding opportunities
            /// - Handle branch target calculation
            /// - Execute MicroOps when available
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PipelineStage_Execute()
            {
                // Move ID Р В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂє EX if EX is empty and ID has data
                if (!pipeEX.Valid && pipeID.Valid)
                {
                    MaterializeExecuteStageLaneState();

                    // Stage 6 Phase B: when the cluster-prepared packet carries widened executable
                    // work, dispatch all materialized lanes via the explicit packet path.
                    if (pipeEX.UsesExplicitPacketLanes &&
                        (pipeEX.MaterializedScalarLaneCount > 1 || pipeEX.SelectedNonScalarSlotMask != 0))
                    {
                        ExecuteExplicitPacketLanes();
                        return;
                    }

                    // Refactoring Pt. 4: Capture ResourceMask and acquire GRLB resources at IDР В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂєEX.
                    // The mask is forwarded through pipeline latches (EXР В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂєMEMР В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂєWB) so that WB can
                    // deterministically release resources, even if the op is squashed.
                    // HLS: ResourceMask is latched into pipeEX D-flip-flops (128 + 64 bit).
                    LatchSingleLaneExecuteDispatchResources();

                    LatchSingleLaneExecuteDispatchState();

                    // Refactoring Pt. 3: Register outstanding Load/Store in per-VT MSHR scoreboard.
                    // Allocates a scoreboard slot at IDР В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂєEX transition so the bank is locked
                    // before the memory request is initiated. The slot index is forwarded
                    // through the pipeline latches (EXР В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂєMEMР В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂєWB) for deterministic clearing.
                    ReserveSingleLaneExecuteMshrScoreboardSlot();

                    if (TryExecuteScalarBranchMicroOp())
                    {
                        return;
                    }

                    if (TryExecuteSingleLaneMicroOp())
                    {
                        return;
                    }

                    RejectSingleLaneReferenceRawFallbackEntry();
                }
            }

            /// <summary>
            /// Stage 4: Memory Access (MEM)
            /// - Perform load/store operations
            /// - Access burst I/O for vector operations
            /// - Setup forwarding for load results
            /// - Refactoring Pt. 5: Domain cert check moved to ID stage (early filtering).
            ///   Operations with mismatched DomainTag are converted to NOP at decode time
            ///   and never reach EX/MEM, eliminating speculative TLB/cache side-channel leaks.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PipelineStage_Memory()
            {
                // Clear EX forwarding path (data moves to MEM stage)
                forwardEX.Clear();

                bool canMoveToMemory = pipeEX.UsesExplicitPacketLanes
                    ? AreAllExplicitExecuteLanesReady(pipeEX)
                    : pipeEX.ResultReady;

                // Move EX Р В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂє MEM if MEM is empty and EX has data ready
                if (!pipeMEM.Valid && pipeEX.Valid && canMoveToMemory)
                {
                    LatchExecuteToMemoryTransferState();

                    if (pipeMEM.UsesExplicitPacketLanes)
                    {
                        ExecuteExplicitPacketMemoryWork();
                        PublishMemoryStageForwarding();

                        return;
                    }

                    // Refactoring Pt. 5: Defense-in-depth Р В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р вЂ™Р’В Р В Р вЂ Р В РІР‚С™Р РЋРІвЂћСћР В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р Р‹Р РЋРЎв„ў domain-violating ops are squashed
                    // at ID stage and should never reach here. If they do (e.g. hot-patched CSR
                    // change between ID and MEM), suppress writes as a safety net.
                    if (TryApplySingleLaneMemoryStageDomainSquash())
                    {
                    }
                    else
                    {
                        PublishSingleLaneMemoryStageResult();
                    }

                    // Setup forwarding path from MEM stage
                    PublishMemoryStageForwarding();
                }
            }

            /// <summary>
            /// Stage 5: Write Back (WB)
            /// - Write results to registers
            /// - Retire instructions (packet-local: all eligible non-faulted lanes per cycle)
            /// - Deliver any remaining stage-aware precise fault after older WB lanes retire
            /// - Update performance counters
            /// - Setup final forwarding path
            /// - Call MicroOp Commit when available
            /// - Refactoring Pt. 5: Domain cert check moved to ID stage.
            ///   WB retains defense-in-depth gate for CSR hot-patch scenarios.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PipelineStage_WriteBack()
            {
                // Clear MEM forwarding path (data moves to WB stage)
                forwardMEM.Clear();
                forwardWB.Clear();

                // Move MEM Р В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В РІР‚в„ўР вЂ™Р’В Р В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р Р†Р вЂљРЎвЂєР РЋРЎвЂє WB if WB is empty and MEM has data
                bool canMoveToWriteBack = pipeMEM.UsesExplicitPacketLanes
                    ? AreAllExplicitMemoryLanesReady(pipeMEM)
                    : pipeMEM.ResultReady;

                if (!pipeWB.Valid && pipeMEM.Valid && canMoveToWriteBack)
                {
                    LatchMemoryToWriteBackTransferState();
                    TryApplyWriteBackStageDomainSquash();
                }

                // Write back to register file and retire Р В Р’В Р вЂ™Р’В Р В Р’В Р Р†Р вЂљР’В Р В Р’В Р вЂ™Р’В Р В Р вЂ Р В РІР‚С™Р РЋРІвЂћСћР В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р Р‹Р РЋРЎв„ў packet-local multi-lane drain
                if (pipeWB.Valid)
                {
                    bool hasRetireWindowExceptionDecision = TryResolveExceptionDeliveryDecisionForRetireWindow(
                        pipeWB,
                        pipeMEM,
                        pipeEX,
                        out PipelineStage retireWindowExceptionStage,
                        out byte retireWindowExceptionLaneIndex,
                        out bool retireWindowShouldSuppressYoungerWork);

                    Span<byte> retireOrder = stackalloc byte[7];
                    int retireLaneCount = ResolveStableRetireOrder(pipeWB, retireOrder);
                    retireLaneCount = TruncateRetireOrderBeforeWriteBackFaultWinner(
                        pipeWB,
                        retireOrder,
                        retireLaneCount,
                        retireWindowExceptionStage,
                        retireWindowExceptionLaneIndex);
                    if (retireLaneCount == 0)
                    {
                        HandleEmptyWriteBackRetireWindow(
                            hasRetireWindowExceptionDecision,
                            retireWindowExceptionStage,
                            retireWindowExceptionLaneIndex,
                            retireWindowShouldSuppressYoungerWork);
                        return;
                    }

                    ulong retireContourPc = pipeWB.GetLane(retireOrder[0]).PC;
                    byte retireContourCarrierMask = BuildRetireSlotMask(retireOrder, retireLaneCount);

                    // The live lane0..5 + lane7 retire window now builds one explicit WB-local typed
                    // retire packet. Every currently authoritative register-producing lane in
                    // that window emits through its MicroOp-owned WB path before authoritative
                    // fault delivery; the WB lane carrier is no longer used as a retire-record
                    // fallback for the live subset.
                    //
                    // Capacity: 4 ALU lanes Р В Р’В Р вЂ™Р’В Р В Р вЂ Р В РІР‚С™Р РЋРЎв„ўР В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р В РІР‚С™Р РЋРЎС™ 2 (MoveMicroOp DT=4 dual-write) + 2 LSU lanes Р В Р’В Р вЂ™Р’В Р В Р вЂ Р В РІР‚С™Р РЋРЎв„ўР В Р’В Р В РІР‚В Р В Р’В Р Р†Р вЂљРЎв„ўР В Р вЂ Р В РІР‚С™Р РЋРЎС™ 1 = 10.
                    // Buffer remains 12 so the lane-7 branch carrier can retire without reopening capacity work.
                    Span<RetireRecord> retireRecords = stackalloc RetireRecord[12];
                    Span<RetireWindowEffect> retireEffects = stackalloc RetireWindowEffect[RetireWindowEffectCapacity];
                    Core.Pipeline.PipelineEvent?[] pipelineEvents = new Core.Pipeline.PipelineEvent?[RetireWindowEffectCapacity];
                    RetireWindowBatch retireBatch = new(
                        retireRecords,
                        retireEffects,
                        pipelineEvents);

                    // Retire all eligible lanes in deterministic ascending lane-index order
                    for (int ri = 0; ri < retireLaneCount; ri++)
                    {
                        byte laneIndex = retireOrder[ri];
                        ScalarWriteBackLaneState lane = pipeWB.GetLane(laneIndex);
                        if (!lane.IsOccupied)
                            continue;

                        pipeWB.ActiveLaneIndex = laneIndex;
                        PublishRetiredWriteBackLaneForwarding(lane);
                        CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane);
                        FinalizeRetiredWriteBackLane(ref retireBatch, laneIndex, lane);
                    }

                    ReadOnlySpan<RetireWindowEffect> capturedRetireEffects = retireBatch.Effects;
                    ApplyRetireBatchImmediateEffects(
                        ref retireBatch,
                        capturedRetireEffects,
                        countRetireCycle: true,
                        out bool hasRetiredPcWrite,
                        out ulong retiredPcWriteTarget,
                        out int retiredPcWriteVtId);

                    if (hasRetireWindowExceptionDecision &&
                        HasRetiredActiveControlFlowRedirect(
                            hasRetiredPcWrite,
                            retiredPcWriteVtId))
                    {
                        pipeCtrl.ExceptionYoungerSuppressCount++;
                        hasRetireWindowExceptionDecision = false;
                        retireWindowExceptionStage = PipelineStage.None;
                        retireWindowExceptionLaneIndex = byte.MaxValue;
                        retireWindowShouldSuppressYoungerWork = false;
                    }

                    FinalizeWriteBackRetireWindow(
                        retireOrder,
                        retireLaneCount,
                        retireContourPc,
                        retireContourCarrierMask,
                        hasRetireWindowExceptionDecision,
                        retireWindowExceptionStage,
                        retireWindowExceptionLaneIndex,
                        retireWindowShouldSuppressYoungerWork,
                        hasRetiredPcWrite,
                        retiredPcWriteTarget,
                        retiredPcWriteVtId,
                        ref retireBatch,
                        capturedRetireEffects);
                }
            }
        }
    }
}


