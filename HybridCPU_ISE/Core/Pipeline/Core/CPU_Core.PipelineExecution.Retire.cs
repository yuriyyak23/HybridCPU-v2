using System;
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishRetiredWriteBackLaneForwarding(in ScalarWriteBackLaneState lane)
            {
                if (!lane.WritesRegister)
                    return;

                forwardWB.Valid = true;
                forwardWB.DestRegID = lane.DestRegID;
                forwardWB.ForwardedValue = lane.ResultValue;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CaptureRetiredWriteBackLaneEffects(
                ref RetireWindowBatch retireBatch,
                byte laneIndex,
                in ScalarWriteBackLaneState lane)
            {
                if (laneIndex >= 6 && laneIndex != 7)
                    return;

                if (lane.MicroOp == null)
                {
                    throw new InvalidOperationException(
                        $"Live WB retire lane {laneIndex} is missing MicroOp authority.");
                }

                int generatedRetireRecordCount = GetGeneratedRetireRecordCount(lane);
                if (generatedRetireRecordCount != 0)
                {
                    ValidateGeneratedRetireLaneContract(lane);
                    AppendLaneGeneratedRetireRecords(ref retireBatch, lane);
                }
                else if (lane.GeneratedCsrEffect is Core.CsrRetireEffect csrEffect)
                {
                    retireBatch.CaptureGeneratedCsrEffect(
                        laneIndex,
                        lane.VirtualThreadId,
                        csrEffect);
                }
                else if (lane.MicroOp is Core.VConfigMicroOp vectorConfigMicroOp)
                {
                    Core.VectorConfigRetireEffect vectorConfigEffect =
                        vectorConfigMicroOp.CreateRetireEffect();
                    if (vectorConfigEffect.IsValid)
                    {
                        retireBatch.CaptureGeneratedVectorConfigEffect(
                            laneIndex,
                            lane.VirtualThreadId,
                            vectorConfigEffect);
                    }
                    else
                    {
                        retireBatch.EmitMicroOpRetireRecords(
                            ref this,
                            lane);
                    }
                }
                else if (lane.GeneratedAtomicEffect is Core.AtomicRetireEffect atomicEffect)
                {
                    retireBatch.CaptureGeneratedAtomicEffect(
                        laneIndex,
                        lane,
                        atomicEffect);
                }
                else
                {
                    retireBatch.EmitMicroOpRetireRecords(
                        ref this,
                        lane);
                }

                if (lane.GeneratedVmxEffect is Core.VmxRetireEffect vmxEffect)
                {
                    retireBatch.CaptureGeneratedVmxEffect(
                        laneIndex,
                        lane,
                        vmxEffect);
                }

                if (lane.MicroOp.SerializationClass is Arch.SerializationClass.FullSerial
                    or Arch.SerializationClass.VmxSerial)
                {
                    retireBatch.NoteSerializingBoundary();
                }

                if (lane.GeneratedEvent != null)
                {
                    Core.SystemEventOrderGuarantee orderGuarantee = lane.MicroOp is Core.SysEventMicroOp systemEventMicroOp
                        ? systemEventMicroOp.OrderGuarantee
                        : ResolveRetiredSystemEventOrderGuaranteeForTesting(lane.GeneratedEvent);

                    if (lane.MicroOp is Core.SysEventMicroOp typedSystemEventMicroOp)
                    {
                        retireBatch.CaptureGeneratedSystemEvent(
                            laneIndex,
                            typedSystemEventMicroOp.EventKind,
                            orderGuarantee,
                            lane.PC,
                            lane.VirtualThreadId);
                    }
                    else
                    {
                        retireBatch.CaptureGeneratedPipelineEvent(
                            laneIndex,
                            lane.GeneratedEvent,
                            orderGuarantee,
                            lane.PC,
                            lane.VirtualThreadId);
                    }
                }

                if (VectorConfig.FSP_Enabled == 1 && lane.MicroOp.OwnerThreadId != 0)
                {
                    VectorConfig.FSP_InjectionCount++;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void FinalizeRetiredWriteBackLane(
                ref RetireWindowBatch retireBatch,
                byte laneIndex,
                in ScalarWriteBackLaneState lane)
            {
                if (lane.GeneratedVmxEffect == null &&
                    lane.GeneratedAtomicEffect == null)
                {
                    RecordTraceEvent(
                        lane.PC,
                        lane.OpCode,
                        ResolveRetiredTraceValue(lane),
                        lane.OwnerThreadId,
                        lane.VirtualThreadId,
                        lane.WasFspInjected,
                        lane.OriginalThreadId);
                }

                pipeCtrl.InstructionsRetired++;
                if (laneIndex < 4)
                {
                    pipeCtrl.ScalarLanesRetired++;
                }
                else
                {
                    pipeCtrl.NonScalarLanesRetired++;
                }

                if (lane.DefersStoreCommitToWriteBack)
                {
                    retireBatch.AppendDeferredStoreLane(laneIndex);
                }

                ReleaseRetiredWriteBackLaneBookkeeping(laneIndex);

                if (lane.WasFspInjected &&
                    lane.OwnerThreadId != lane.OriginalThreadId &&
                    _fspScheduler != null)
                {
                    _fspScheduler.ReleaseSpeculationBudget();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetireBatchImmediateEffects(
                ref RetireWindowBatch retireBatch,
                ReadOnlySpan<RetireWindowEffect> capturedRetireEffects,
                bool countRetireCycle,
                out bool hasRetiredPcWrite,
                out ulong retiredPcWriteTarget,
                out int retiredPcWriteVtId)
            {
                hasRetiredPcWrite = TryResolveSingleRetiredPcWrite(
                    retireBatch.RetireRecords,
                    out retiredPcWriteTarget,
                    out retiredPcWriteVtId);

                if (countRetireCycle)
                {
                    pipeCtrl.RetireCycleCount++;
                }

                RetireCoordinator.Retire(retireBatch.RetireRecords);

                for (int i = 0; i < capturedRetireEffects.Length; i++)
                {
                    RetireWindowEffect retireEffect = capturedRetireEffects[i];
                    switch (retireEffect.Kind)
                    {
                        case RetireWindowEffectKind.DeferredStoreCommit:
                            ApplyRetiredScalarStoreCommit(
                                pipeWB.GetLane(retireEffect.DeferredStoreLaneIndex));
                            break;

                        case RetireWindowEffectKind.Csr:
                            ApplyRetiredCsrEffect(retireEffect.CsrEffect);
                            break;

                        case RetireWindowEffectKind.VectorConfig:
                            ApplyRetiredVectorConfigEffect(retireEffect.VectorConfigEffect);
                            break;

                        case RetireWindowEffectKind.Atomic:
                        {
                            // Atomic retirement is intentionally split: first apply the bound
                            // memory-side effect and reservation discipline, then publish any
                            // returned architectural destination value through RetireCoordinator.
                            Core.AtomicRetireOutcome retiredAtomicOutcome =
                                ApplyRetiredAtomicEffect(retireEffect.AtomicEffect);
                            if (retiredAtomicOutcome.HasRegisterWriteback)
                            {
                                RetireCoordinator.Retire(
                                    RetireRecord.RegisterWrite(
                                        retireEffect.AtomicEffect.VirtualThreadId,
                                        retiredAtomicOutcome.RegisterDestination,
                                        retiredAtomicOutcome.RegisterWritebackValue));
                            }

                            if (retireEffect.HasAtomicTraceLane)
                            {
                                RecordTraceEvent(
                                    retireEffect.AtomicTraceLane.PC,
                                    retireEffect.AtomicTraceLane.OpCode,
                                    retiredAtomicOutcome.HasRegisterWriteback
                                        ? retiredAtomicOutcome.RegisterWritebackValue
                                        : retireEffect.AtomicTraceLane.FallbackValue,
                                    retireEffect.AtomicTraceLane.OwnerThreadId,
                                    retireEffect.AtomicTraceLane.VirtualThreadId,
                                    retireEffect.AtomicTraceLane.WasFspInjected,
                                    retireEffect.AtomicTraceLane.OriginalThreadId);
                            }

                            break;
                        }

                        case RetireWindowEffectKind.ScalarMemoryStore:
                            ApplyRetiredScalarStoreCommit(
                                retireEffect.MemoryAddress,
                                retireEffect.MemoryData,
                                retireEffect.MemoryAccessSize,
                                "Retire batch direct scalar store");
                            break;

                        case RetireWindowEffectKind.PredicateState:
                            SetPredicateRegister(
                                retireEffect.PredicateRegisterId,
                                retireEffect.PredicateMaskValue);
                            break;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HasRetiredActiveControlFlowRedirect(
                bool hasRetiredPcWrite,
                int retiredPcWriteVtId)
            {
                return hasRetiredPcWrite &&
                    NormalizePipelineStateVtId(retiredPcWriteVtId) == ReadActiveVirtualThreadId();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void FinalizeWriteBackRetireWindow(
                Span<byte> retireOrder,
                int retireLaneCount,
                ulong retireContourPc,
                byte retireContourCarrierMask,
                bool hasRetireWindowExceptionDecision,
                PipelineStage retireWindowExceptionStage,
                byte retireWindowExceptionLaneIndex,
                bool retireWindowShouldSuppressYoungerWork,
                bool hasRetiredPcWrite,
                ulong retiredPcWriteTarget,
                int retiredPcWriteVtId,
                ref RetireWindowBatch retireBatch,
                ReadOnlySpan<RetireWindowEffect> capturedRetireEffects)
            {
                if (hasRetireWindowExceptionDecision)
                {
                    ClearRetiredWriteBackLaneStateBeforeFaultDelivery(retireOrder, retireLaneCount);
                    DeliverStageAwareRetireWindowFault(
                        retireWindowExceptionStage,
                        retireWindowExceptionLaneIndex,
                        retireWindowShouldSuppressYoungerWork);
                }

                ClearRetiredWriteBackLanes(retireOrder, retireLaneCount);
                ApplyRetireBatchLateEffectsAndRedirect(
                    ref retireBatch,
                    capturedRetireEffects,
                    hasRetiredPcWrite,
                    retiredPcWriteTarget,
                    retiredPcWriteVtId);

                if (retireLaneCount != 0 && retireContourCarrierMask == 0)
                {
                    throw new InvalidOperationException(
                        "WB retire window finalized a non-empty retire set with an empty retire-visibility carrier mask.");
                }

                PublishRetireVisibilityContourCertificate(
                    Core.PipelineContourOwner.WriteBackRetireWindow,
                    Core.PipelineContourVisibilityStage.WriteBack,
                    retireContourPc,
                    retireContourCarrierMask);

                if (retireLaneCount != 0 &&
                    (!retireVisibilityCertificate.IsPublished ||
                     retireVisibilityCertificate.Owner != Core.PipelineContourOwner.WriteBackRetireWindow ||
                     retireVisibilityCertificate.VisibilityStage != Core.PipelineContourVisibilityStage.WriteBack ||
                     retireVisibilityCertificate.Pc != retireContourPc ||
                     retireVisibilityCertificate.SlotMask != retireContourCarrierMask))
                {
                    throw new InvalidOperationException(
                        "WB retire window failed to publish the finalized retire-visibility contour certificate.");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetireBatchLateEffectsAndRedirect(
                ref RetireWindowBatch retireBatch,
                ReadOnlySpan<RetireWindowEffect> capturedRetireEffects,
                bool hasRetiredPcWrite,
                ulong retiredPcWriteTarget,
                int retiredPcWriteVtId)
            {
                for (int i = 0; i < capturedRetireEffects.Length; i++)
                {
                    RetireWindowEffect retireEffect = capturedRetireEffects[i];
                    if (retireEffect.Kind != RetireWindowEffectKind.Vmx)
                    {
                        continue;
                    }

                    Core.VmxRetireOutcome retiredVmxOutcome = ApplyRetiredVmxEffect(
                        retireEffect.VmxEffect,
                        retireEffect.VmxEffectVtId);
                    retireBatch.AccumulateAssistBoundaryKilled(retiredVmxOutcome.FlushesPipeline);

                    if (retireEffect.HasVmxTraceLane)
                    {
                        RecordTraceEvent(
                            retireEffect.VmxTraceLane.PC,
                            retireEffect.VmxTraceLane.OpCode,
                            retiredVmxOutcome.HasRegisterWriteback
                                ? retiredVmxOutcome.RegisterWritebackValue
                                : retireEffect.VmxTraceLane.FallbackValue,
                            retireEffect.VmxTraceLane.OwnerThreadId,
                            retireEffect.VmxTraceLane.VirtualThreadId,
                            retireEffect.VmxTraceLane.WasFspInjected,
                            retireEffect.VmxTraceLane.OriginalThreadId);
                    }
                }

                for (int i = 0; i < capturedRetireEffects.Length; i++)
                {
                    RetireWindowEffect retireEffect = capturedRetireEffects[i];
                    if (retireEffect.Kind != RetireWindowEffectKind.PipelineEvent)
                    {
                        continue;
                    }

                    retireBatch.AccumulateAssistBoundaryKilled(
                        HandleRetiredSystemEventBoundary(
                            retireBatch.GetPipelineEventPayload(retireEffect.PipelineEventSlot),
                            retireEffect.SystemEventOrderGuarantee,
                            retireEffect.SystemEventPc,
                            retireEffect.SystemEventVtId));
                }

                for (int i = 0; i < capturedRetireEffects.Length; i++)
                {
                    RetireWindowEffect retireEffect = capturedRetireEffects[i];
                    if (retireEffect.Kind != RetireWindowEffectKind.System)
                    {
                        continue;
                    }

                    retireBatch.AccumulateAssistBoundaryKilled(
                        HandleRetiredSystemEventBoundary(
                            retireEffect.SystemEventKind,
                            retireEffect.SystemEventOrderGuarantee,
                            retireEffect.SystemEventPc,
                            retireEffect.SystemEventVtId));
                }

                for (int i = 0; i < capturedRetireEffects.Length; i++)
                {
                    if (capturedRetireEffects[i].Kind != RetireWindowEffectKind.SerializingBoundary)
                    {
                        continue;
                    }

                    HandleRetiredSerializingBoundary(retireBatch.AssistBoundaryKilledThisRetireWindow);
                }

                if (HasRetiredActiveControlFlowRedirect(
                        hasRetiredPcWrite,
                        retiredPcWriteVtId))
                {
                    FlushPipeline(Core.AssistInvalidationReason.PipelineFlush);
                    RedirectActiveExecutionForControlFlow(retiredPcWriteTarget);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void HandleRetiredSerializingBoundary(bool assistBoundaryKilledThisRetireWindow)
            {
                if (_loopBuffer.CurrentReplayPhase.IsActive)
                {
                    _loopBuffer.Invalidate(Core.ReplayPhaseInvalidationReason.SerializingEvent);
                    _fspScheduler?.SetReplayPhaseContext(
                        _loopBuffer.CurrentReplayPhase,
                        invalidateAssistOnDeactivate: false);
                }

                _fspScheduler?.NotifySerializingCommit(invalidateAssist: false);
                if (!assistBoundaryKilledThisRetireWindow)
                {
                    InvalidateAssistRuntime(Core.AssistInvalidationReason.SerializingBoundary);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveSingleRetiredPcWrite(
                ReadOnlySpan<RetireRecord> retireRecords,
                out ulong retiredPcWriteTarget,
                out int retiredPcWriteVtId)
            {
                retiredPcWriteTarget = 0;
                retiredPcWriteVtId = 0;
                bool hasRetiredPcWrite = false;

                for (int i = 0; i < retireRecords.Length; i++)
                {
                    RetireRecord retireRecord = retireRecords[i];
                    if (!retireRecord.IsPcWrite)
                    {
                        continue;
                    }

                    if (hasRetiredPcWrite)
                    {
                        throw new InvalidOperationException(
                            "Retire packet published multiple PcWrite records. Control-flow redirect authority must remain singular per retire window.");
                    }

                    hasRetiredPcWrite = true;
                    retiredPcWriteTarget = retireRecord.Value;
                    retiredPcWriteVtId = retireRecord.VtId;
                }

                return hasRetiredPcWrite;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetiredSystemEvent(
                Core.SystemEventKind systemEventKind,
                ulong retiredPc,
                int virtualThreadId)
            {
                int normalizedVtId = NormalizePipelineStateVtId(virtualThreadId);
                if (systemEventKind == Core.SystemEventKind.Sev)
                {
                    for (int vt = 0; vt < SmtWays; vt++)
                    {
                        bool isIssuer = vt == normalizedVtId;
                        if (!isIssuer &&
                            ReadVirtualThreadPipelineState(vt) != YAKSys_Hybrid_CPU.Core.PipelineState.WaitForEvent)
                        {
                            continue;
                        }

                        ApplySystemEventKindToVirtualThread(
                            systemEventKind,
                            vt,
                            isIssuer ? retiredPc : null);
                    }

                    return;
                }

                ApplySystemEventKindToVirtualThread(systemEventKind, normalizedVtId, retiredPc);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetiredSystemEvent(
                Core.Pipeline.PipelineEvent systemEvent,
                ulong retiredPc,
                int virtualThreadId)
            {
                ArgumentNullException.ThrowIfNull(systemEvent);
                ApplyRetiredPipelineEvent(
                    systemEvent,
                    retiredPc,
                    virtualThreadId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetiredPipelineEvent(
                Core.Pipeline.PipelineEvent pipelineEvent,
                ulong retiredPc,
                int virtualThreadId)
            {
                ArgumentNullException.ThrowIfNull(pipelineEvent);
                int normalizedVtId = NormalizePipelineStateVtId(virtualThreadId);
                if (pipelineEvent is Core.Pipeline.SevEvent)
                {
                    for (int vt = 0; vt < SmtWays; vt++)
                    {
                        bool isIssuer = vt == normalizedVtId;
                        if (!isIssuer &&
                            ReadVirtualThreadPipelineState(vt) != YAKSys_Hybrid_CPU.Core.PipelineState.WaitForEvent)
                        {
                            continue;
                        }

                        ApplyPipelineEventToVirtualThread(
                            pipelineEvent,
                            vt,
                            isIssuer ? retiredPc : null);
                    }

                    return;
                }

                ApplyPipelineEventToVirtualThread(pipelineEvent, normalizedVtId, retiredPc);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HandleRetiredSystemEventBoundary(
                Core.SystemEventKind systemEventKind,
                Core.SystemEventOrderGuarantee orderGuarantee,
                ulong retiredPc,
                int virtualThreadId)
            {
                Core.AssistInvalidationReason invalidationReason =
                    ResolveAssistInvalidationReasonForRetiredSystemEvent(systemEventKind, orderGuarantee);

                if (RequiresRetiredSystemEventPipelineFlush(orderGuarantee))
                {
                    FlushPipeline(invalidationReason == Core.AssistInvalidationReason.None
                        ? Core.AssistInvalidationReason.PipelineFlush
                        : invalidationReason);
                }
                else if (invalidationReason != Core.AssistInvalidationReason.None)
                {
                    InvalidateAssistRuntime(invalidationReason);
                }

                ApplyRetiredSystemEvent(systemEventKind, retiredPc, virtualThreadId);
                return invalidationReason != Core.AssistInvalidationReason.None;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HandleRetiredSystemEventBoundary(
                Core.Pipeline.PipelineEvent systemEvent,
                Core.SystemEventOrderGuarantee orderGuarantee,
                ulong retiredPc,
                int virtualThreadId)
            {
                ArgumentNullException.ThrowIfNull(systemEvent);
                return HandleRetiredPipelineEventBoundary(
                    systemEvent,
                    orderGuarantee,
                    retiredPc,
                    virtualThreadId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HandleRetiredPipelineEventBoundary(
                Core.Pipeline.PipelineEvent pipelineEvent,
                Core.SystemEventOrderGuarantee orderGuarantee,
                ulong retiredPc,
                int virtualThreadId)
            {
                ArgumentNullException.ThrowIfNull(pipelineEvent);
                Core.AssistInvalidationReason invalidationReason =
                    ResolveAssistInvalidationReasonForRetiredPipelineEvent(
                        pipelineEvent,
                        orderGuarantee);

                if (RequiresRetiredSystemEventPipelineFlush(orderGuarantee))
                {
                    FlushPipeline(invalidationReason == Core.AssistInvalidationReason.None
                        ? Core.AssistInvalidationReason.PipelineFlush
                        : invalidationReason);
                }
                else if (invalidationReason != Core.AssistInvalidationReason.None)
                {
                    InvalidateAssistRuntime(invalidationReason);
                }

                ApplyRetiredPipelineEvent(pipelineEvent, retiredPc, virtualThreadId);
                return invalidationReason != Core.AssistInvalidationReason.None;
            }

            internal void ApplyRetiredSystemEventForTesting(
                Core.Pipeline.PipelineEvent systemEvent,
                int virtualThreadId,
                ulong retiredPc = 0)
            {
                HandleRetiredSystemEventBoundary(
                    systemEvent,
                    ResolveRetiredSystemEventOrderGuaranteeForTesting(systemEvent),
                    retiredPc,
                    virtualThreadId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplySystemEventKindToVirtualThread(
                Core.SystemEventKind systemEventKind,
                int virtualThreadId,
                ulong? retiredPc)
            {
                int normalizedVtId = NormalizePipelineStateVtId(virtualThreadId);
                PrivilegeLevel privilege = ResolveRetiredSystemEventPrivilege(normalizedVtId);
                LiveCpuStateAdapter liveState = CreateLiveCpuStateAdapter(normalizedVtId);
                if (retiredPc.HasValue)
                {
                    liveState.WritePc((byte)normalizedVtId, retiredPc.Value);
                }

                Core.Pipeline.PipelineFsmEventHandler eventHandler = new(
                    Csr,
                    SmtWays);

                YAKSys_Hybrid_CPU.Core.PipelineState nextState = eventHandler.Handle(
                    CreateRetiredSystemEvent(systemEventKind, normalizedVtId),
                    ReadVirtualThreadPipelineState(normalizedVtId),
                    liveState,
                    privilege);

                liveState.SetCurrentPipelineState(nextState);
                liveState.ApplyTo(ref this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyPipelineEventToVirtualThread(
                Core.Pipeline.PipelineEvent pipelineEvent,
                int virtualThreadId,
                ulong? retiredPc)
            {
                ArgumentNullException.ThrowIfNull(pipelineEvent);
                int normalizedVtId = NormalizePipelineStateVtId(virtualThreadId);
                PrivilegeLevel privilege = ResolveRetiredSystemEventPrivilege(normalizedVtId);
                LiveCpuStateAdapter liveState = CreateLiveCpuStateAdapter(normalizedVtId);
                if (retiredPc.HasValue)
                {
                    liveState.WritePc((byte)normalizedVtId, retiredPc.Value);
                }

                Core.Pipeline.PipelineFsmEventHandler eventHandler = new(
                    Csr,
                    SmtWays);

                YAKSys_Hybrid_CPU.Core.PipelineState nextState = eventHandler.Handle(
                    CreateRetiredPipelineEvent(pipelineEvent, normalizedVtId),
                    ReadVirtualThreadPipelineState(normalizedVtId),
                    liveState,
                    privilege);

                liveState.SetCurrentPipelineState(nextState);
                liveState.ApplyTo(ref this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private PrivilegeLevel ResolveRetiredSystemEventPrivilege(int virtualThreadId)
            {
                int normalizedVtId = NormalizePipelineStateVtId(virtualThreadId);
                return (uint)normalizedVtId < (uint)ArchContexts.Length
                    ? ArchContexts[normalizedVtId].CurrentPrivilege
                    : PrivilegeLevel.Machine;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool RequiresRetiredSystemEventPipelineFlush(Core.SystemEventOrderGuarantee orderGuarantee)
            {
                return orderGuarantee == Core.SystemEventOrderGuarantee.FlushPipeline ||
                       orderGuarantee == Core.SystemEventOrderGuarantee.FullSerialTrapBoundary;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.AssistInvalidationReason ResolveAssistInvalidationReasonForRetiredSystemEvent(
                Core.SystemEventKind systemEventKind,
                Core.SystemEventOrderGuarantee orderGuarantee)
            {
                return systemEventKind switch
                {
                    Core.SystemEventKind.Fence or Core.SystemEventKind.FenceI
                        => Core.AssistInvalidationReason.Fence,
                    Core.SystemEventKind.Ecall or
                    Core.SystemEventKind.Ebreak or
                    Core.SystemEventKind.Mret or
                    Core.SystemEventKind.Sret
                        => Core.AssistInvalidationReason.Trap,
                    _ when orderGuarantee == Core.SystemEventOrderGuarantee.FullSerialTrapBoundary
                        => Core.AssistInvalidationReason.Trap,
                    _ => Core.AssistInvalidationReason.None
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.AssistInvalidationReason ResolveAssistInvalidationReasonForRetiredSystemEvent(
                Core.Pipeline.PipelineEvent systemEvent,
                Core.SystemEventOrderGuarantee orderGuarantee)
            {
                ArgumentNullException.ThrowIfNull(systemEvent);
                return ResolveAssistInvalidationReasonForRetiredSystemEvent(
                    ResolveRetiredSystemEventKind(systemEvent),
                    orderGuarantee);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.AssistInvalidationReason ResolveAssistInvalidationReasonForRetiredPipelineEvent(
                Core.Pipeline.PipelineEvent pipelineEvent,
                Core.SystemEventOrderGuarantee orderGuarantee)
            {
                ArgumentNullException.ThrowIfNull(pipelineEvent);

                return pipelineEvent switch
                {
                    Core.Pipeline.FenceEvent or
                    Core.Pipeline.EcallEvent or
                    Core.Pipeline.EbreakEvent or
                    Core.Pipeline.MretEvent or
                    Core.Pipeline.SretEvent or
                    Core.Pipeline.WfiEvent or
                    Core.Pipeline.WfeEvent or
                    Core.Pipeline.SevEvent
                        => ResolveAssistInvalidationReasonForRetiredSystemEvent(
                            ResolveRetiredSystemEventKind(pipelineEvent),
                            orderGuarantee),
                    _ when orderGuarantee == Core.SystemEventOrderGuarantee.FullSerialTrapBoundary
                        => Core.AssistInvalidationReason.Trap,
                    _ => Core.AssistInvalidationReason.None
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.SystemEventOrderGuarantee ResolveRetiredSystemEventOrderGuaranteeForTesting(
                Core.Pipeline.PipelineEvent systemEvent)
            {
                return systemEvent switch
                {
                    Core.Pipeline.FenceEvent fenceEvent
                        => fenceEvent.IsInstructionFence
                            ? Core.SystemEventOrderGuarantee.FlushPipeline
                            : Core.SystemEventOrderGuarantee.DrainMemory,
                    Core.Pipeline.EcallEvent or
                    Core.Pipeline.EbreakEvent or
                    Core.Pipeline.TrapEntryEvent or
                    Core.Pipeline.MretEvent or
                    Core.Pipeline.SretEvent
                        => Core.SystemEventOrderGuarantee.FullSerialTrapBoundary,
                    Core.Pipeline.WfiEvent or
                    Core.Pipeline.WfeEvent or
                    Core.Pipeline.SevEvent
                        => Core.SystemEventOrderGuarantee.DrainMemory,
                    Core.Pipeline.YieldEvent or
                    Core.Pipeline.PodBarrierEvent or
                    Core.Pipeline.VtBarrierEvent
                        => Core.SystemEventOrderGuarantee.None,
                    _ => Core.SystemEventOrderGuarantee.None
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.SystemEventKind ResolveRetiredSystemEventKind(
                Core.Pipeline.PipelineEvent systemEvent)
            {
                ArgumentNullException.ThrowIfNull(systemEvent);

                return systemEvent switch
                {
                    Core.Pipeline.FenceEvent fenceEvent
                        => fenceEvent.IsInstructionFence
                            ? Core.SystemEventKind.FenceI
                            : Core.SystemEventKind.Fence,
                    Core.Pipeline.EcallEvent => Core.SystemEventKind.Ecall,
                    Core.Pipeline.EbreakEvent => Core.SystemEventKind.Ebreak,
                    Core.Pipeline.MretEvent => Core.SystemEventKind.Mret,
                    Core.Pipeline.SretEvent => Core.SystemEventKind.Sret,
                    Core.Pipeline.WfiEvent => Core.SystemEventKind.Wfi,
                    Core.Pipeline.WfeEvent => Core.SystemEventKind.Wfe,
                    Core.Pipeline.SevEvent => Core.SystemEventKind.Sev,
                    Core.Pipeline.YieldEvent => Core.SystemEventKind.Yield,
                    Core.Pipeline.PodBarrierEvent => Core.SystemEventKind.PodBarrier,
                    Core.Pipeline.VtBarrierEvent => Core.SystemEventKind.VtBarrier,
                    _ => throw new InvalidOperationException(
                        $"Unsupported retired system event payload {systemEvent.GetType().Name}.")
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Pipeline.PipelineEvent CreateRetiredSystemEvent(
                Core.SystemEventKind systemEventKind,
                int virtualThreadId)
            {
                byte vtId = (byte)(virtualThreadId & 0xFF);

                return systemEventKind switch
                {
                    Core.SystemEventKind.Fence => new Core.Pipeline.FenceEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0,
                        IsInstructionFence = false
                    },
                    Core.SystemEventKind.FenceI => new Core.Pipeline.FenceEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0,
                        IsInstructionFence = true
                    },
                    Core.SystemEventKind.Ecall => new Core.Pipeline.EcallEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0,
                        EcallCode = 0
                    },
                    Core.SystemEventKind.Ebreak => new Core.Pipeline.EbreakEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.Mret => new Core.Pipeline.MretEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.Sret => new Core.Pipeline.SretEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.Wfi => new Core.Pipeline.WfiEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.Wfe => new Core.Pipeline.WfeEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.Sev => new Core.Pipeline.SevEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.Yield => new Core.Pipeline.YieldEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.PodBarrier => new Core.Pipeline.PodBarrierEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    Core.SystemEventKind.VtBarrier => new Core.Pipeline.VtBarrierEvent
                    {
                        VtId = vtId,
                        BundleSerial = 0
                    },
                    _ => throw new InvalidOperationException(
                        $"Unsupported retired system event kind {systemEventKind}.")
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Pipeline.PipelineEvent CreateRetiredPipelineEvent(
                Core.Pipeline.PipelineEvent pipelineEvent,
                int virtualThreadId)
            {
                ArgumentNullException.ThrowIfNull(pipelineEvent);
                byte vtId = (byte)(virtualThreadId & 0xFF);

                return pipelineEvent switch
                {
                    Core.Pipeline.FenceEvent fenceEvent => new Core.Pipeline.FenceEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial,
                        IsInstructionFence = fenceEvent.IsInstructionFence
                    },
                    Core.Pipeline.EcallEvent ecallEvent => new Core.Pipeline.EcallEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial,
                        EcallCode = ecallEvent.EcallCode
                    },
                    Core.Pipeline.EbreakEvent => new Core.Pipeline.EbreakEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.TrapEntryEvent trapEntryEvent => new Core.Pipeline.TrapEntryEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial,
                        CauseCode = trapEntryEvent.CauseCode,
                        FaultAddress = trapEntryEvent.FaultAddress
                    },
                    Core.Pipeline.MretEvent => new Core.Pipeline.MretEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.SretEvent => new Core.Pipeline.SretEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.WfiEvent => new Core.Pipeline.WfiEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.YieldEvent => new Core.Pipeline.YieldEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.WfeEvent => new Core.Pipeline.WfeEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.SevEvent => new Core.Pipeline.SevEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.PodBarrierEvent => new Core.Pipeline.PodBarrierEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    Core.Pipeline.VtBarrierEvent => new Core.Pipeline.VtBarrierEvent
                    {
                        VtId = vtId,
                        BundleSerial = pipelineEvent.BundleSerial
                    },
                    _ => throw new InvalidOperationException(
                        $"Unsupported retired pipeline event payload {pipelineEvent.GetType().Name}.")
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.Pipeline.PipelineEvent? MaterializeLaneGeneratedEvent(Core.MicroOp microOp)
            {
                if (microOp is Core.SysEventMicroOp systemEventMicroOp)
                {
                    return systemEventMicroOp.CreatePipelineEvent(ref this);
                }

                if (microOp is Core.TrapMicroOp trapMicroOp)
                {
                    return trapMicroOp.CreatePipelineEvent();
                }

                return null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.CsrRetireEffect? MaterializeLaneCsrEffect(Core.MicroOp microOp)
            {
                if (microOp is Core.CSRMicroOp csrMicroOp)
                {
                    return csrMicroOp.CreateRetireEffect(ref this);
                }

                return null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.AtomicRetireEffect? MaterializeLaneAtomicEffect(Core.MicroOp microOp)
            {
                if (microOp is Core.AtomicMicroOp atomicMicroOp)
                {
                    return atomicMicroOp.CreateRetireEffect();
                }

                return null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsAtomicReadOnlyAlignmentFaultCarrier(Core.MicroOp? microOp)
            {
                return microOp is Core.AtomicMicroOp atomicMicroOp &&
                    atomicMicroOp.OpCode is IsaOpcodeValues.LR_W or IsaOpcodeValues.LR_D;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveLanePrimaryWriteBackValue(
                Core.MicroOp microOp,
                Core.CsrRetireEffect? csrEffect,
                out ulong value)
            {
                if (csrEffect.HasValue && csrEffect.Value.HasRegisterWriteback)
                {
                    value = csrEffect.Value.ReadValue;
                    return true;
                }

                return microOp.TryGetPrimaryWriteBackResult(out value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void EmitGeneratedCsrRetireRecords(
                int virtualThreadId,
                in Core.CsrRetireEffect csrEffect,
                Span<RetireRecord> retireRecords,
                ref int retireRecordCount)
            {
                if (!csrEffect.HasRegisterWriteback)
                    return;

                AppendRetireRecord(
                    retireRecords,
                    ref retireRecordCount,
                    RetireRecord.RegisterWrite(
                        virtualThreadId,
                        csrEffect.DestRegId,
                        csrEffect.ReadValue));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void EmitGeneratedVectorConfigRetireRecords(
                int virtualThreadId,
                in Core.VectorConfigRetireEffect vectorConfigEffect,
                Span<RetireRecord> retireRecords,
                ref int retireRecordCount)
            {
                if (!vectorConfigEffect.HasRegisterWriteback)
                    return;

                AppendRetireRecord(
                    retireRecords,
                    ref retireRecordCount,
                    RetireRecord.RegisterWrite(
                        virtualThreadId,
                        vectorConfigEffect.DestinationRegister,
                        vectorConfigEffect.ActualVectorLength));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetiredCsrEffect(
                in Core.CsrRetireEffect csrEffect)
            {
                if (csrEffect.ClearsArchitecturalExceptionState)
                {
                    ExceptionStatus.ClearExceptionCounters();
                }

                if (csrEffect.HasCsrWrite)
                {
                    Core.CSRMicroOp.WriteCsr(
                        ref this,
                        csrEffect.StorageSurface,
                        csrEffect.CsrAddress,
                        csrEffect.CsrWriteValue);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetiredVectorConfigEffect(
                in Core.VectorConfigRetireEffect vectorConfigEffect)
            {
                VectorConfig.VL = vectorConfigEffect.ActualVectorLength;
                VectorConfig.VTYPE = vectorConfigEffect.VType;
                VectorConfig.TailAgnostic = vectorConfigEffect.TailAgnostic;
                VectorConfig.MaskAgnostic = vectorConfigEffect.MaskAgnostic;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.AtomicRetireOutcome ApplyRetiredAtomicEffect(
                in Core.AtomicRetireEffect atomicEffect)
            {
                if (atomicEffect.CoreId != unchecked((ushort)CoreID))
                {
                    throw new InvalidOperationException(
                        $"Atomic retire effect belongs to core {atomicEffect.CoreId} but is being applied on core {CoreID}.");
                }

                return AtomicMemoryUnit.ApplyRetireEffect(atomicEffect);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ApplyCapturedRetireWindowBatch(
                ref RetireWindowBatch retireBatch,
                bool countRetireCycle = false)
            {
                ReadOnlySpan<RetireWindowEffect> capturedRetireEffects = retireBatch.Effects;
                ApplyRetireBatchImmediateEffects(
                    ref retireBatch,
                    capturedRetireEffects,
                    countRetireCycle,
                    out bool hasRetiredPcWrite,
                    out ulong retiredPcWriteTarget,
                    out int retiredPcWriteVtId);
                ApplyRetireBatchLateEffectsAndRedirect(
                    ref retireBatch,
                    capturedRetireEffects,
                    hasRetiredPcWrite,
                    retiredPcWriteTarget,
                    retiredPcWriteVtId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void AppendRetireRecord(
                Span<RetireRecord> retireRecords,
                ref int retireRecordCount,
                in RetireRecord retireRecord)
            {
                if ((uint)retireRecordCount >= (uint)retireRecords.Length)
                {
                    throw new InvalidOperationException("WB retire record buffer exhausted.");
                }

                retireRecords[retireRecordCount++] = retireRecord;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GetGeneratedRetireRecordCount(in ScalarWriteBackLaneState lane)
            {
                int count = lane.GeneratedRetireRecordCount;
                if ((uint)count > GeneratedRetireRecordCapacity)
                {
                    throw new InvalidOperationException(
                        $"WB retire lane {lane.LaneIndex} published {count} generated retire records, exceeding capacity {GeneratedRetireRecordCapacity}.");
                }

                return count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static RetireRecord GetGeneratedRetireRecord(
                in ScalarWriteBackLaneState lane,
                int index)
            {
                return index switch
                {
                    0 => lane.GeneratedRetireRecord0,
                    1 => lane.GeneratedRetireRecord1,
                    _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Generated retire record index is outside the fixed lane capacity.")
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AppendLaneGeneratedRetireRecords(
                ref RetireWindowBatch retireBatch,
                in ScalarWriteBackLaneState lane)
            {
                int count = GetGeneratedRetireRecordCount(lane);
                for (int i = 0; i < count; i++)
                {
                    RetireRecord retireRecord = GetGeneratedRetireRecord(lane, i);
                    if (retireRecord.VtId != lane.VirtualThreadId)
                    {
                        throw new InvalidOperationException(
                            $"WB retire lane {lane.LaneIndex} published generated retire record VT{retireRecord.VtId} but carrier VT is {lane.VirtualThreadId}.");
                    }

                    retireBatch.AppendRetireRecord(retireRecord);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ValidateGeneratedRetireLaneContract(in ScalarWriteBackLaneState lane)
            {
                if (GetGeneratedRetireRecordCount(lane) == 0)
                    return;

                if (lane.GeneratedEvent != null ||
                    lane.GeneratedCsrEffect.HasValue ||
                    lane.GeneratedAtomicEffect.HasValue ||
                    lane.GeneratedVmxEffect.HasValue)
                {
                    throw new InvalidOperationException(
                        $"WB retire lane {lane.LaneIndex} cannot mix generated retire records with typed generated effects.");
                }

                if (lane.MicroOp == null)
                {
                    throw new InvalidOperationException(
                        $"WB retire lane {lane.LaneIndex} published generated retire records without MicroOp authority.");
                }

                Span<RetireRecord> probeRetireRecords = stackalloc RetireRecord[4];
                int probeRetireRecordCount = 0;
                lane.MicroOp.CapturePrimaryWriteBackResult(lane.ResultValue);
                lane.MicroOp.EmitWriteBackRetireRecords(
                    ref this,
                    probeRetireRecords,
                    ref probeRetireRecordCount);
                if (probeRetireRecordCount != 0)
                {
                    throw new InvalidOperationException(
                        $"WB retire lane {lane.LaneIndex} cannot mix MicroOp-owned retire emission with generated retire records.");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong ResolveRetiredTraceValue(in ScalarWriteBackLaneState lane)
            {
                int count = GetGeneratedRetireRecordCount(lane);
                for (int i = count - 1; i >= 0; i--)
                {
                    RetireRecord retireRecord = GetGeneratedRetireRecord(lane, i);
                    if (retireRecord.Kind == RetireRecordKind.RegisterWrite)
                    {
                        return retireRecord.Value;
                    }
                }

                return lane.ResultValue;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AppendGeneratedExecutingLaneRetireRecord(in RetireRecord retireRecord)
            {
                if (!pipeEX.Valid)
                {
                    throw new InvalidOperationException(
                        "Generated retire records require a live execute-stage carrier.");
                }

                byte laneIndex = pipeEX.ActiveLaneIndex;
                if (!(laneIndex < 6 || laneIndex == 7))
                {
                    throw new InvalidOperationException(
                        $"Generated retire record publication requires a retire-visible lane carrier; lane {laneIndex} is not retire-visible.");
                }

                ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                if (!lane.IsOccupied || lane.MicroOp == null)
                {
                    throw new InvalidOperationException(
                        $"Execute lane {laneIndex} cannot publish generated retire records without occupied MicroOp authority.");
                }

                if (retireRecord.VtId != lane.VirtualThreadId)
                {
                    throw new InvalidOperationException(
                        $"Execute lane {laneIndex} cannot publish generated retire record VT{retireRecord.VtId} on carrier VT{lane.VirtualThreadId}.");
                }

                switch (lane.GeneratedRetireRecordCount)
                {
                    case 0:
                        lane.GeneratedRetireRecord0 = retireRecord;
                        break;

                    case 1:
                        lane.GeneratedRetireRecord1 = retireRecord;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Execute lane {laneIndex} generated retire record buffer exhausted.");
                }

                lane.GeneratedRetireRecordCount++;
                pipeEX.SetLane(laneIndex, lane);
            }
        }
    }
}
