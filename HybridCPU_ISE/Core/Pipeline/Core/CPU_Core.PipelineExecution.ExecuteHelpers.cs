using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseCompletedExecuteNonScalarLane(byte laneIndex)
            {
                if (laneIndex < 4 || laneIndex >= 8)
                    return;

                ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                if (!lane.IsOccupied)
                    return;

                byte slotMask = lane.SlotIndex < 8
                    ? (byte)(1 << lane.SlotIndex)
                    : (byte)0;

                ReleaseScalarLaneBookkeeping(lane);

                ScalarExecuteLaneState clearedLane = new();
                clearedLane.Clear(laneIndex);
                pipeEX.SetLane(laneIndex, clearedLane);
                pipeEX.SelectedNonScalarSlotMask = (byte)(pipeEX.SelectedNonScalarSlotMask & ~slotMask);
                pipeEX.MaterializedScalarLaneCount = CountOccupiedScalarExecuteLanes(pipeEX);
                pipeEX.MaterializedPhysicalLaneCount = CountOccupiedPhysicalExecuteLanes(pipeEX);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordLane7ConditionalBranchCompletion(bool redirected)
            {
                pipeCtrl.Lane7ConditionalBranchExecuteCompletionCount++;
                if (redirected)
                {
                    pipeCtrl.Lane7ConditionalBranchRedirectCount++;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyExplicitPacketAssistExecutionOutcome(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                bool success,
                ref int executedPhysicalLaneCount)
            {
                Core.AssistMicroOp? assistMicroOp = lane.MicroOp as Core.AssistMicroOp;

                lane.IsMemoryOp = true;
                lane.IsLoad = true;
                lane.MemoryAddress = assistMicroOp?.BaseAddress ?? 0;
                lane.MemoryAccessSize = assistMicroOp?.ElementSize ?? (byte)0;
                lane.ResultReady = success;
                lane.GeneratedEvent = null;
                lane.GeneratedCsrEffect = null;
                lane.GeneratedAtomicEffect = null;
                lane.GeneratedVmxEffect = null;
                lane.GeneratedRetireRecordCount = 0;
                lane.GeneratedRetireRecord0 = default;
                lane.GeneratedRetireRecord1 = default;
                lane.VectorComplete = success;
                lane.ResultValue = 0;

                pipeEX.SetLane(laneIndex, lane);
                executedPhysicalLaneCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyExplicitPacketExecuteEpilogueAccounting(
                int executedPhysicalLaneCount,
                int executedScalarLaneCount)
            {
                if (executedPhysicalLaneCount > 1)
                {
                    pipeCtrl.MultiLaneExecuteCount++;

                    // Stage 7 Phase C: track partial-width issue (2 or 3 lanes, not full 4)
                    if (executedScalarLaneCount > 1 && executedScalarLaneCount < 4)
                    {
                        pipeCtrl.PartialWidthIssueCount++;
                    }
                }

                // Stage 6 Phase E: record actual issue width in histogram
                if (pipeCtrl.ScalarIssueWidthHistogram != null &&
                    executedScalarLaneCount >= 0 &&
                    executedScalarLaneCount <= 4)
                {
                    pipeCtrl.ScalarIssueWidthHistogram[executedScalarLaneCount]++;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishExplicitPacketExecuteForwarding()
            {
                // Multi-lane forwarding: lowest-index occupied lane with WritesRegister && ResultReady.
                for (byte laneIndex = 0; laneIndex < 4; laneIndex++)
                {
                    ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                    if (lane.IsOccupied && lane.WritesRegister && lane.ResultReady)
                    {
                        forwardEX.Valid = true;
                        forwardEX.DestRegID = lane.DestRegID;
                        forwardEX.ForwardedValue = lane.ResultValue;
                        forwardEX.AvailableCycle = (long)pipeCtrl.CycleCount + 1;
                        forwardEX.SourceStage = PipelineStage.Execute;
                        break;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryConsumeEmptyExplicitPacketAfterExecution()
            {
                if (CountOccupiedPhysicalExecuteLanes(pipeEX) != 0)
                    return false;

                pipeEX.Clear();
                ConsumeDecodeStateAfterExecuteDispatch();
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CompleteExplicitPacketExecuteDispatch(byte originalActiveLaneIndex)
            {
                pipeEX.Valid = true;
                pipeEX.ActiveLaneIndex = originalActiveLaneIndex;
                ConsumeDecodeStateAfterExecuteDispatch();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishSingleLaneExecuteForwarding(bool includeTimingMetadata)
            {
                if (!pipeEX.WritesRegister || !pipeEX.ResultReady)
                    return;

                forwardEX.Valid = true;
                forwardEX.DestRegID = pipeEX.DestRegID;
                forwardEX.ForwardedValue = pipeEX.ResultValue;

                if (includeTimingMetadata)
                {
                    // Phase 2: canonical EX->EX forwarding becomes visible on the next cycle.
                    forwardEX.AvailableCycle = (long)pipeCtrl.CycleCount + 1;
                    forwardEX.SourceStage = PipelineStage.Execute;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RejectSingleLaneReferenceRawFallbackEntry()
            {
                FailCloseSingleLaneExecuteAfterNonFaultException();

                throw Core.ExecutionFaultContract.CreateWrappedException(
                    Core.ExecutionFaultCategory.InvalidInternalOp,
                    $"Scalar opcode 0x{pipeID.OpCode:X} reached execute without an authoritative MicroOp. " +
                    "Production pipeline execution no longer enters reference raw fallback for null-MicroOp carriers; reference raw execution is test-only.",
                    new InvalidOperationException("Missing authoritative MicroOp for production single-lane execute."));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LatchSingleLaneExecuteDispatchResources()
            {
                Core.ResourceBitset idResourceMask = pipeID.MicroOp?.ResourceMask ?? Core.ResourceBitset.Zero;
                ulong idResourceToken = 0;
                if (idResourceMask.IsNonZero)
                {
                    AcquireResourcesWithToken(idResourceMask, out idResourceToken);
                }

                pipeEX.ResourceMask = idResourceMask;
                pipeEX.ResourceToken = idResourceToken;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LatchSingleLaneExecuteDispatchState()
            {
                pipeEX.PC = pipeID.PC;
                pipeEX.OpCode = pipeID.OpCode;
                pipeEX.IsMemoryOp = pipeID.IsMemoryOp;
                pipeEX.IsVectorOp = pipeID.IsVectorOp;
                pipeEX.WritesRegister = pipeID.WritesRegister;
                ushort destRegId = ResolvePrimaryWriteRegister(pipeID.MicroOp);
                if (pipeID.MicroOp is null && pipeID.WritesRegister)
                {
                    destRegId = pipeID.Reg1ID;
                }

                pipeEX.DestRegID = destRegId;
                pipeEX.MicroOp = pipeID.MicroOp;
                pipeEX.OwnerThreadId = pipeID.MicroOp?.OwnerThreadId ?? 0;
                pipeEX.VirtualThreadId = pipeID.MicroOp?.VirtualThreadId ?? 0;
                pipeEX.OwnerContextId = pipeID.MicroOp?.OwnerContextId ?? 0;
                pipeEX.WasFspInjected = pipeID.MicroOp?.IsFspInjected ?? false;
                pipeEX.OriginalThreadId = pipeID.MicroOp?.OwnerThreadId ?? 0;
                pipeEX.AdmissionExecutionMode = pipeID.AdmissionExecutionMode;
                pipeEX.DomainTag = pipeID.MicroOp?.Placement.DomainTag ?? 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReserveSingleLaneExecuteMshrScoreboardSlot()
            {
                pipeEX.MshrScoreboardSlot = -1;
                pipeEX.MshrVirtualThreadId = pipeID.MicroOp?.VirtualThreadId ?? 0;
                if (pipeID.MicroOp is Core.LoadStoreMicroOp loadStoreMicroOp && _fspScheduler != null)
                {
                    int bankId = loadStoreMicroOp.MemoryBankId;
                    int vtId = loadStoreMicroOp.VirtualThreadId;
                    Core.ScoreboardEntryType entryType = loadStoreMicroOp is Core.LoadMicroOp
                        ? Core.ScoreboardEntryType.OutstandingLoad
                        : Core.ScoreboardEntryType.OutstandingStore;
                    int slot = _fspScheduler.SetSmtScoreboardPendingTyped(
                        bankId,
                        vtId,
                        (long)pipeCtrl.CycleCount,
                        entryType,
                        bankId);
                    pipeEX.MshrScoreboardSlot = slot;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteSingleLaneMicroOp()
            {
                if (pipeEX.MicroOp == null)
                    return false;

                try
                {
                    // MicroOp.Execute() is the current execution path; future work may route
                    // all execution through ExecutionDispatcherV4.Dispatch(InstructionIR, state).
                    bool success = pipeEX.MicroOp.Execute(ref this);

                    if (success)
                    {
                        pipeEX.VectorComplete = true;
                        pipeEX.ResultReady = true;
                        pipeEX.GeneratedEvent = MaterializeLaneGeneratedEvent(pipeEX.MicroOp);
                        pipeEX.GeneratedCsrEffect = MaterializeLaneCsrEffect(pipeEX.MicroOp);
                        pipeEX.GeneratedAtomicEffect = MaterializeLaneAtomicEffect(pipeEX.MicroOp);
                        pipeEX.GeneratedVmxEffect = MaterializeLaneVmxEffect(pipeEX.MicroOp);
                        if (pipeEX.GeneratedAtomicEffect.HasValue)
                        {
                            pipeEX.WritesRegister = false;
                        }

                        if (pipeEX.MicroOp is Core.TrapMicroOp)
                        {
                            pipeEX.IsMemoryOp = false;
                            pipeEX.IsLoad = false;
                            pipeEX.MemoryAddress = 0;
                        }
                        else if (pipeEX.MicroOp is Core.VectorMicroOp)
                        {
                            // Completed vector/stream MicroOps already consumed their
                            // memory-side semantics in the authoritative EX contour.
                            // Keep legacy single-lane MEM from reopening raw scalar
                            // load/store follow-through for the same carrier.
                            pipeEX.IsMemoryOp = false;
                        }

                        pipeEX.ResultValue = TryResolveLanePrimaryWriteBackValue(
                            pipeEX.MicroOp,
                            pipeEX.GeneratedCsrEffect,
                            out ulong writeBackValue)
                            ? writeBackValue
                            : 0;
                    }
                    else
                    {
                        // MicroOp needs to stall/retry
                        pipeEX.ResultReady = false;
                        pipeEX.GeneratedEvent = null;
                        pipeEX.GeneratedCsrEffect = null;
                        pipeEX.GeneratedAtomicEffect = null;
                        pipeEX.GeneratedVmxEffect = null;
                        ScalarExecuteLaneState stalledLane = pipeEX.GetLane(pipeEX.ActiveLaneIndex);
                        stalledLane.GeneratedRetireRecordCount = 0;
                        stalledLane.GeneratedRetireRecord0 = default;
                        stalledLane.GeneratedRetireRecord1 = default;
                        pipeEX.SetLane(pipeEX.ActiveLaneIndex, stalledLane);
                    }

                    pipeEX.Valid = true;
                    PublishExecuteCompletionContourCertificate(
                        Core.PipelineContourOwner.SingleLaneMicroOpExecution,
                        Core.PipelineContourVisibilityStage.Execute,
                        pipeEX.PC,
                        (byte)(1 << pipeEX.ActiveLaneIndex));
                    ConsumeDecodeStateAfterExecuteDispatch();
                    PublishSingleLaneExecuteForwarding(includeTimingMetadata: true);
                    return true;
                }
                catch (Core.PageFaultException pageFaultException)
                {
                    DeliverStageAwareExecutePageFault(pageFaultException);
                    throw;
                }
                catch (Core.Memory.MemoryAlignmentException memoryAlignmentException)
                {
                    Core.PageFaultException alignmentFault = new(
                        memoryAlignmentException.Message,
                        memoryAlignmentException,
                        memoryAlignmentException.Address,
                        isWrite: !IsAtomicReadOnlyAlignmentFaultCarrier(pipeEX.MicroOp));
                    DeliverStageAwareExecutePageFault(alignmentFault);
                    throw alignmentFault;
                }
                catch (Exception ex) when (pipeID.IsVectorOp)
                {
                    FailCloseSingleLaneExecuteAfterNonFaultException();

                    if (Core.ExecutionFaultContract.TryGetCategory(ex, out Core.ExecutionFaultCategory category))
                    {
                        throw Core.ExecutionFaultContract.CreateWrappedException(
                            category,
                        $"Vector opcode 0x{pipeID.OpCode:X} reached reference raw execute fallback after MicroOp failure. " +
                        "Mainline vector/stream execution must not collapse into direct StreamEngine execution or silent continue outside the MicroOp-owned runtime contour; pipeline execution remains authoritative through the explicit MicroOp lane.",
                        ex);
                    }

                    throw new InvalidOperationException(
                        $"Vector opcode 0x{pipeID.OpCode:X} reached reference raw execute fallback after MicroOp failure. " +
                        "Mainline vector/stream execution must not collapse into direct StreamEngine execution or silent continue outside the MicroOp-owned runtime contour; pipeline execution remains authoritative through the explicit MicroOp lane.",
                        ex);
                }
                catch (Exception ex)
                {
                    FailCloseSingleLaneExecuteAfterNonFaultException();

                    Core.ExecutionFaultCategory category =
                        Core.ExecutionFaultContract.TryGetCategory(ex, out Core.ExecutionFaultCategory propagatedCategory)
                            ? propagatedCategory
                            : Core.ExecutionFaultCategory.InvalidInternalOp;

                    throw Core.ExecutionFaultContract.CreateWrappedException(
                        category,
                        $"Scalar opcode 0x{pipeID.OpCode:X} threw a non-fault MicroOp exception inside the authoritative pipeline runtime contour. " +
                        "Production execution no longer falls back to reference raw execution for this failure class.",
                        ex);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteExplicitPacketAssistLane(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                ref int executedPhysicalLaneCount)
            {
                if (!lane.MicroOp!.IsAssist)
                    return false;

                bool success = lane.MicroOp.Execute(ref this);
                lane = pipeEX.GetLane(laneIndex);

                ApplyExplicitPacketAssistExecutionOutcome(
                    laneIndex,
                    ref lane,
                    success,
                    ref executedPhysicalLaneCount);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordExecuteLaneTraceEvent(byte laneIndex, ScalarExecuteLaneState lane)
            {
                HybridCPU_ISE.Core.TraceSink? traceSink = Processor.TraceSink;
                if (traceSink == null)
                    return;

                var evt = new HybridCPU_ISE.Core.TraceEvent(
                    (long)lane.PC,
                    (int)(lane.PC / 256),
                    (int)((lane.PC % 256) / 32),
                    lane.OpCode)
                {
                    Result = lane.ResultValue,
                    ExceptionCount = this.ExceptionStatus.TotalExceptions()
                };

                traceSink.Record(evt);

                if (traceSink.ShouldCaptureFullState)
                {
                    traceSink.RecordPhaseAwareState(
                        new HybridCPU_ISE.Core.FullStateTraceEvent
                        {
                            PC = (long)lane.PC,
                            BundleId = (int)(lane.PC / 256),
                            OpIndex = (int)((lane.PC % 256) / 32),
                            Opcode = lane.OpCode,
                            ThreadId = lane.WasFspInjected ? 0 : lane.OwnerThreadId,
                            CycleNumber = (long)pipeCtrl.CycleCount,
                            RegisterFile = CaptureTraceRegisterFile(lane.OwnerThreadId),
                            PredicateRegisters = CaptureTracePredicateRegisters(),
                            WasStolenSlot = lane.WasFspInjected,
                            OriginalThreadId = lane.OriginalThreadId,
                            PipelineStage = $"EX-L{laneIndex}",
                            Stalled = pipeCtrl.Stalled,
                            StallReason = PipelineStallText.Render(pipeCtrl.StallReason, PipelineStallTextStyle.Trace),
                            ActiveMemoryRequests = GetBoundMemorySubsystemCurrentQueuedRequests(),
                            MemorySubsystemCycle = 0,
                            ThreadReadyQueueDepths = _fspScheduler == null ? null : new[]
                            {
                                _fspScheduler.GetOutstandingMemoryCount(0),
                                _fspScheduler.GetOutstandingMemoryCount(1),
                                _fspScheduler.GetOutstandingMemoryCount(2),
                                _fspScheduler.GetOutstandingMemoryCount(3)
                            },
                            CurrentFSPPolicy = _loopBuffer.CurrentReplayPhase.IsActive ? "ReplayAwarePhase1" : "DeterministicFSP"
                        },
                        _loopBuffer.CurrentReplayPhase,
                        _fspScheduler?.GetPhaseMetrics() ?? default,
                        phaseCertificateTemplateReusable: _loopBuffer.CurrentReplayPhase.IsActive &&
                            (_fspScheduler?.LastPhaseCertificateInvalidationReason ?? Core.ReplayPhaseInvalidationReason.None) == Core.ReplayPhaseInvalidationReason.None);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldUseExplicitPacketGenericMicroOpExecutionContour(
                byte laneIndex,
                Core.MicroOp microOp)
            {
                return laneIndex < 4 ||
                    microOp is Core.TrapMicroOp ||
                    (laneIndex == 7 &&
                     !microOp.IsMemoryOp &&
                     (!microOp.IsControlFlow || microOp is Core.TrapMicroOp));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyExplicitPacketGenericMicroOpExecutionOutcome(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                bool success,
                ref int executedPhysicalLaneCount,
                ref int executedScalarLaneCount)
            {
                lane.ResultReady = success;
                lane.GeneratedEvent = success
                    ? MaterializeLaneGeneratedEvent(lane.MicroOp)
                    : null;
                lane.GeneratedCsrEffect = success
                    ? MaterializeLaneCsrEffect(lane.MicroOp)
                    : null;
                lane.GeneratedAtomicEffect = success
                    ? MaterializeLaneAtomicEffect(lane.MicroOp)
                    : null;
                lane.GeneratedVmxEffect = success
                    ? MaterializeLaneVmxEffect(lane.MicroOp)
                    : null;
                if (lane.GeneratedAtomicEffect.HasValue)
                {
                    lane.WritesRegister = false;
                }

                if (success && lane.MicroOp is Core.TrapMicroOp)
                {
                    // Canonical-known trapped auxiliaries may carry memory/control
                    // admission facts for placement truth, but once the trap event
                    // materializes they must not reopen LSU/control follow-through
                    // in downstream MEM.
                    lane.IsMemoryOp = false;
                    lane.IsLoad = false;
                    lane.MemoryAccessSize = 0;
                    lane.MemoryAddress = 0;
                }
                else if (success && lane.MicroOp is Core.VectorMicroOp)
                {
                    // Completed vector/stream MicroOps already own their memory-side
                    // follow-through in EX. Downstream MEM must not reopen legacy
                    // scalar load/store handling for the same authoritative carrier.
                    lane.IsMemoryOp = false;
                }

                if (success)
                {
                    lane.VectorComplete = true;
                    lane.ResultValue = TryResolveLanePrimaryWriteBackValue(
                        lane.MicroOp,
                        lane.GeneratedCsrEffect,
                        out ulong writeBackValue)
                        ? writeBackValue
                        : 0;
                }
                else
                {
                    lane.GeneratedRetireRecordCount = 0;
                    lane.GeneratedRetireRecord0 = default;
                    lane.GeneratedRetireRecord1 = default;
                }

                pipeEX.SetLane(laneIndex, lane);
                if (laneIndex < 4)
                    executedScalarLaneCount++;
                executedPhysicalLaneCount++;

                // Stage 6 Phase E: per-lane EX trace
                RecordExecuteLaneTraceEvent(laneIndex, lane);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteExplicitPacketGenericMicroOpLane(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                ref int executedPhysicalLaneCount,
                ref int executedScalarLaneCount)
            {
                if (!ShouldUseExplicitPacketGenericMicroOpExecutionContour(laneIndex, lane.MicroOp!))
                    return false;

                // MicroOp.Execute() is the current execution path; future work may route
                // all execution through ExecutionDispatcherV4.Dispatch(InstructionIR, state).
                bool success = lane.MicroOp!.Execute(ref this);
                lane = pipeEX.GetLane(laneIndex);

                ApplyExplicitPacketGenericMicroOpExecutionOutcome(
                    laneIndex,
                    ref lane,
                    success,
                    ref executedPhysicalLaneCount,
                    ref executedScalarLaneCount);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void FailCloseSingleLaneExecuteAfterNonFaultException()
            {
                ReleaseExecuteStageLaneAwareBookkeeping();
                pipeEX.Clear();
                forwardEX.Clear();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void FailCloseExplicitPacketLaneAfterNonFaultExecutionException(
                ref ScalarExecuteLaneState lane)
            {
                lane.ResultReady = false;
                lane.MicroOp = null;
                pipeEX.SetLane(lane.LaneIndex, lane);
            }
        }
    }
}
