using HybridCPU_ISE.Arch;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Execute one pipeline cycle - advance all stages
            ///
            /// Pipeline execution order (backwards to avoid overwriting):
            /// 5. WriteBack stage
            /// 4. Memory stage
            /// 3. Execute stage
            /// 2. Decode stage
            /// 1. Fetch stage
            ///
            /// This ordering ensures each stage sees the state from the previous cycle.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ExecutePipelineCycle()
            {
                if (!pipeCtrl.Enabled) return;

                pipeCtrl.CycleCount++;

                GetBoundMemorySubsystem()?.AdvanceCycles(1);

                RefreshInFlightExplicitMemoryProgress();

                PipelineCycleStallDecision hazardStallDecision = ResolvePipelineHazardStallDecision();
                if (hazardStallDecision.ShouldStall)
                {
                    ApplyPipelineCycleStallDecision(hazardStallDecision);
                    return;
                }

                pipeCtrl.Stalled = false;

                PipelineStage_WriteBack();
                PipelineStage_Memory();
                PipelineStage_Execute();
                DecodeStageResult decodeStageResult = PipelineStage_Decode();
                if (decodeStageResult.ShouldStall)
                {
                    ApplyPipelineCycleStallDecision(
                        PipelineCycleStallDecision.FromDecode(decodeStageResult));
                    return;
                }

                PipelineStage_Fetch();

                RecordPhaseTimelineSample("CYCLE");

                Core.ReplayPhaseContext replayPhaseBeforeEndCycle = _loopBuffer.CurrentReplayPhase;
                _loopBuffer.EndCycle();
                PublishReplayPhaseDeactivationToSchedulerIfNeeded(replayPhaseBeforeEndCycle);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyPipelineCycleStallDecision(in PipelineCycleStallDecision stallDecision)
            {
                if (!stallDecision.ShouldStall)
                {
                    return;
                }

                pipeCtrl.Stalled = true;
                pipeCtrl.StallReason = stallDecision.StallReason;
                pipeCtrl.StallCycles++;

                if (stallDecision.CountMemoryStall)
                {
                    pipeCtrl.MemoryStalls++;
                }

                if (stallDecision.CountInvariantViolation)
                {
                    pipeCtrl.InvariantViolationCount++;
                }

                if (stallDecision.CountMshrScoreboardStall)
                {
                    pipeCtrl.MshrScoreboardStalls++;
                }

                if (stallDecision.CountBankConflictStall)
                {
                    pipeCtrl.BankConflictStallCycles++;
                }

                RecordPhaseTimelineSample("STALL");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishReplayPhaseDeactivationToSchedulerIfNeeded(
                in Core.ReplayPhaseContext replayPhaseBefore)
            {
                Core.ReplayPhaseContext replayPhaseAfter = _loopBuffer.CurrentReplayPhase;
                if (!replayPhaseBefore.IsActive ||
                    replayPhaseAfter.IsActive ||
                    replayPhaseAfter.LastInvalidationReason == Core.ReplayPhaseInvalidationReason.None)
                {
                    return;
                }

                _fspScheduler?.SetReplayPhaseContext(replayPhaseAfter);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void PublishReplayPhaseContextIfNeeded(
                Core.MicroOpScheduler? scheduler,
                in Core.ReplayPhaseContext replayPhase)
            {
                if (scheduler == null)
                {
                    return;
                }

                if (replayPhase.IsActive ||
                    !ReplayPhaseContextsMatch(scheduler.CurrentReplayPhase, replayPhase))
                {
                    scheduler.SetReplayPhaseContext(replayPhase);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ReplayPhaseContextsMatch(
                in Core.ReplayPhaseContext left,
                in Core.ReplayPhaseContext right)
            {
                return left.IsActive == right.IsActive &&
                       left.EpochId == right.EpochId &&
                       left.CachedPc == right.CachedPc &&
                       left.EpochLength == right.EpochLength &&
                       left.CompletedReplays == right.CompletedReplays &&
                       left.ValidSlotCount == right.ValidSlotCount &&
                       left.StableDonorMask == right.StableDonorMask &&
                       left.LastInvalidationReason == right.LastInvalidationReason;
            }

            /// <summary>
            /// Stage 1: Instruction Fetch (IF)
            /// - Fetch VLIW bundle from memory
            /// - Update PC
            /// - Prefetch next bundle if possible
            ///
            /// VLIW Variant A: One bundle fetched, then 8 slots decoded sequentially.
            /// IP advances by 256 only when all 8 slots of the bundle have been decoded.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PipelineStage_Fetch()
            {
                if (pipeIF.Valid)
                    return;

                ulong fetchPC = ReadActiveLivePc();

                if (fetchPC >= GetBoundMainMemoryLength())
                {
                    pipeIF.Valid = false;
                    return;
                }

                Core.ReplayPhaseContext replayPhaseBeforeTryReplay = _loopBuffer.CurrentReplayPhase;
                _replayFetchBuffer ??= new Core.MicroOp?[Core.BundleMetadata.BundleSlotCount];
                Core.MicroOp?[] replayTarget = _replayFetchBuffer;
                if (_loopBuffer.TryReplay(fetchPC, replayTarget))
                {
                    pipeIF.PC = fetchPC;
                    pipeIF.VLIWBundle = null;
                    pipeIF.BundleAnnotations = null;
                    pipeIF.HasBundleAnnotations = false;
                    pipeIF.Valid = true;
                    pipeIF.PrefetchComplete = true;
                    pipelineBundleSlot = 0;
                    bundleDecodedAndPacked = true;
                    LoadCurrentDecodedBundleSlotCarrierFromCarriers(fetchPC, replayTarget);
                    return;
                }

                PublishReplayPhaseDeactivationToSchedulerIfNeeded(replayPhaseBeforeTryReplay);

                pipeIF.PC = fetchPC;
                _fetchVliwBuffer ??= new byte[256];
                Cache_VLIWBundle_Object fetchedBundle = GetVLIWBundleByPointer(fetchPC);
                pipeIF.VLIWBundle = _fetchVliwBuffer;
                fetchedBundle.VLIWCache_VLIWBundle.CopyTo(_fetchVliwBuffer, 0);
                pipeIF.BundleAnnotations = fetchedBundle.VLIWCache_BundleAnnotations;
                pipeIF.HasBundleAnnotations = fetchedBundle.VLIWCache_HasAnnotationCarrier;
                pipeIF.Valid = true;
                pipeIF.PrefetchComplete = true;
                pipelineBundleSlot = 0;
                bundleDecodedAndPacked = false;
                ResetCurrentDecodedBundleSlotCarrierState(fetchPC);

                ulong nextPC = fetchPC + 256;
                if (nextPC < GetBoundMainMemoryLength())
                {
                    PrefetchVLIWBundle(nextPC);
                }
            }

            /// <summary>
            /// Stage 2: Instruction Decode (ID)
            /// - Decode instruction from VLIW bundle
            /// - Read register operands
            /// - Detect instruction type (ALU, memory, control)
            /// - Create MicroOp from InstructionRegistry if available
            /// - Apply FSP packing when enabled (full bundle decode required)
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private DecodeStageResult PipelineStage_Decode()
            {
                if (!pipeID.Valid && pipeIF.Valid)
                {
                    ResetDecodeAdmissionStateForBundle(pipeIF.PC);

                    if (!bundleDecodedAndPacked)
                    {
                        DecodeFullBundle();

                        ReadCurrentForegroundDecodedBundleTransportFacts(
                            out _,
                            out Core.DecodedBundleSlotDescriptor[] decodedBundleSlots,
                            out _,
                            out _);

                        if (decodedBundleSlots.Length == 8)
                        {
                            ulong maxIterations = ResolveCurrentLoopBufferMaxIterations(decodedBundleSlots);
                            if (maxIterations > 1)
                            {
                                _loopBuffer.BeginLoad(pipeIF.PC, maxIterations);
                                for (int i = 0; i < 8; i++)
                                {
                                    _loopBuffer.StoreSlot(i, decodedBundleSlots[i].MicroOp);
                                }

                                _loopBuffer.CommitLoad();
                            }
                        }

                        bundleDecodedAndPacked = true;
                    }

                    ReadCurrentForegroundDecodedBundleTransportFacts(
                        out ulong currentBundlePc,
                        out Core.DecodedBundleSlotDescriptor[] currentBundleSlots,
                        out Core.DecodedBundleAdmissionPrep currentAdmissionPrep,
                        out Core.DecodedBundleDependencySummary? currentDependencySummary);

                    if (VectorConfig.FSP_Enabled == 1 &&
                        !CurrentDecodedBundleHasDecodeFault(currentBundlePc))
                    {
                        int currentThreadId =
                            ResolveDecodedBundleOwnerVirtualThreadId(
                                currentBundleSlots);
                        byte donorMask = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            if (_loopBuffer.IsSlotDonorEligible(i))
                            {
                                donorMask |= (byte)(1 << i);
                            }
                        }

                        System.Collections.Generic.IReadOnlyList<Core.MicroOp?> packedBundle = ApplyFSPPacking(
                            currentBundlePc,
                            currentBundleSlots,
                            currentAdmissionPrep,
                            currentDependencySummary,
                            currentThreadId,
                            donorMask: donorMask);
                        if (packedBundle != null && packedBundle.Count == 8)
                        {
                            PublishCurrentFspDerivedIssuePlan(
                                currentBundlePc,
                                packedBundle);
                            ReadCurrentForegroundDecodedBundleTransportFacts(
                                out currentBundlePc,
                                out currentBundleSlots,
                                out currentAdmissionPrep,
                                out currentDependencySummary);
                        }
                    }

                    Core.DecodedBundleSlotDescriptor[] executionBundleSlots =
                        BuildForegroundExecutionSlotView(
                            currentBundlePc,
                            currentBundleSlots);
                    Core.DecodedBundleAdmissionPrep executionAdmissionPrep = currentAdmissionPrep;
                    Core.DecodedBundleDependencySummary? executionDependencySummary = currentDependencySummary;
                    AlignForegroundExecutionAdmissionFacts(
                        currentBundlePc,
                        currentBundleSlots,
                        executionBundleSlots,
                        ref executionAdmissionPrep,
                        ref executionDependencySummary);
                    Core.DecodedBundleSlotDescriptor currentSlotDescriptor = executionBundleSlots[pipelineBundleSlot];
                    Core.MicroOp slotMicroOp = currentSlotDescriptor.MicroOp;

                    while (ShouldSkipDecodedSlotForForegroundIssue(currentSlotDescriptor))
                    {
                        pipeCtrl.NopElisionSkipCount++;
                        AdvanceBundleSlot();
                        if (pipelineBundleSlot >= 8 || !pipeIF.Valid)
                        {
                            return DecodeStageResult.NoProgress(
                                currentBundlePc,
                                DecodeStageDiagnosticsKind.NoInput);
                        }

                        ReadCurrentForegroundDecodedBundleTransportFacts(
                            out currentBundlePc,
                            out currentBundleSlots,
                            out currentAdmissionPrep,
                            out currentDependencySummary);
                        executionBundleSlots = BuildForegroundExecutionSlotView(
                            currentBundlePc,
                            currentBundleSlots);
                        executionAdmissionPrep = currentAdmissionPrep;
                        executionDependencySummary = currentDependencySummary;
                        AlignForegroundExecutionAdmissionFacts(
                            currentBundlePc,
                            currentBundleSlots,
                            executionBundleSlots,
                            ref executionAdmissionPrep,
                            ref executionDependencySummary);
                        currentSlotDescriptor = executionBundleSlots[pipelineBundleSlot];
                        slotMicroOp = currentSlotDescriptor.MicroOp;
                    }

                    ScalarExceptionOrderingDecision exceptionDecision =
                        ResolveDecodeExceptionOrderingDecision(in currentSlotDescriptor, pipeIF.PC);
                    if (exceptionDecision.DeliveryKind != ScalarExceptionDeliveryKind.None)
                    {
                        pipeCtrl.EarlyDomainSquashCount++;

                        if (exceptionDecision.IsSilentSpeculativeSquash)
                        {
                            PublishCurrentForegroundSlotMutation(
                                pipelineBundleSlot,
                                CreateSilentSpeculativeSquashReplacement(
                                    currentSlotDescriptor.GetRuntimeExecutionOpCode()));
                        }
                        else
                        {
                            FlushPipeline(Core.AssistInvalidationReason.Trap);
                            throw CreatePreciseDecodeException(exceptionDecision);
                        }

                        AdvanceBundleSlot();
                        return DecodeStageResult.NoProgress(
                            currentBundlePc,
                            DecodeStageDiagnosticsKind.SilentSpeculativeSquash,
                            rejectedSlots: 1);
                    }

                    if (TryResolveDecodeLocalStall(
                        in currentSlotDescriptor,
                        currentBundlePc,
                        out DecodeStageResult decodeLocalStall))
                    {
                        return decodeLocalStall;
                    }

                    pipeID.PC = pipeIF.PC;
                    pipeID.SlotIndex = pipelineBundleSlot;
                    pipeID.MicroOp = slotMicroOp;
                    pipeID.OpCode = currentSlotDescriptor.GetRuntimeExecutionOpCode();

                    pipeIDClusterPreparation = Core.ClusterIssuePreparation.Create(
                        currentBundlePc,
                        executionBundleSlots,
                        executionAdmissionPrep,
                        executionDependencySummary);
                    pipeIDAdmissionPreparation = Core.RuntimeClusterAdmissionPreparation.Create(pipeIDClusterPreparation);
                    pipeIDAdmissionCandidateView = Core.RuntimeClusterAdmissionCandidateView.Create(
                        currentBundlePc,
                        executionBundleSlots,
                        pipeIDClusterPreparation,
                        pipeIDAdmissionPreparation);
                    pipeIDAdmissionDecisionDraft = Core.RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                        pipeIDAdmissionCandidateView,
                        pipeCtrl.ClusterPreparedModeEnabled)
                        .BindToCurrentSlot(pipelineBundleSlot);
                    pipeIDAdmissionHandoff = Core.RuntimeClusterAdmissionHandoff.Create(
                        currentBundlePc,
                        executionBundleSlots,
                        pipeIDClusterPreparation,
                        pipeIDAdmissionCandidateView,
                        pipeIDAdmissionDecisionDraft);

                    EnforceDecodedSlotExecutionSurfaceContract(
                        currentSlotDescriptor,
                        currentBundlePc);

                    RecordClusterPathProbeDiagnostics(pipeIDAdmissionCandidateView, pipeIDAdmissionDecisionDraft);
                    RecordDifferentialTraceEntry(pipeIDAdmissionCandidateView, pipeIDAdmissionDecisionDraft);

                    if (pipelineBundleSlot == 0)
                    {
                        RecordExecutableClusterAdmissionChoice(pipeIDAdmissionDecisionDraft);
                        RecordDecoderModernizationTelemetry(
                            currentBundleSlots,
                            pipeIDClusterPreparation,
                            pipeIDAdmissionCandidateView);
                    }

                    pipeID.Reg1ID = 0;
                    pipeID.Reg2ID = 0;
                    pipeID.Reg3ID = 0;
                    pipeID.AuxData = 0;
                    pipeID.IsVectorOp = currentSlotDescriptor.IsVectorOp;
                    pipeID.IsBranchOp = currentSlotDescriptor.GetRuntimeAdmissionIsControlFlow();
                    pipeID.IsMemoryOp = currentSlotDescriptor.GetRuntimeAdmissionIsMemoryOp();
                    pipeID.WritesRegister = currentSlotDescriptor.GetRuntimeAdmissionWritesRegister();
                    pipeID.AdmissionExecutionMode = pipeIDAdmissionDecisionDraft.ExecutionMode;

                    IReadOnlyList<int> readRegisters = currentSlotDescriptor.GetRuntimeAdmissionReadRegisters();
                    if (readRegisters != null && readRegisters.Count > 0)
                    {
                        pipeID.Reg2ID = (ushort)readRegisters[0];
                        if (readRegisters.Count > 1)
                        {
                            pipeID.Reg3ID = (ushort)readRegisters[1];
                        }
                    }

                    IReadOnlyList<int> writeRegisters = currentSlotDescriptor.GetRuntimeAdmissionWriteRegisters();
                    if (writeRegisters != null && writeRegisters.Count > 0)
                    {
                        pipeID.Reg1ID = (ushort)writeRegisters[0];
                    }

                    if (currentSlotDescriptor.GetRuntimeAdmissionIsControlFlow() &&
                        slotMicroOp is Core.NopMicroOp)
                    {
                        FlushPipeline(Core.AssistInvalidationReason.Trap);
                        throw Core.UnsupportedExecutionSurfaceException.CreateForControlFlow(
                            pipeID.SlotIndex,
                            pipeID.OpCode,
                            pipeID.PC);
                    }

                    pipeID.Valid = true;

                    byte issuedSlots = 1;
                    if (pipeIDAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource)
                    {
                        Core.BundleIssuePacket issuePacket = pipeIDAdmissionHandoff.IssuePacket;
                        byte executableNonScalarPhysicalLaneMask =
                            ResolveExecutableNonScalarPhysicalLaneMask(
                                issuePacket,
                                pipeIDAdmissionHandoff.DependencySummary);
                        int consumedCount = ResolveConsumedSlotCount(
                            issuePacket,
                            executableNonScalarPhysicalLaneMask);
                        issuedSlots = consumedCount > 0
                            ? (byte)Math.Min(consumedCount, byte.MaxValue)
                            : (byte)1;
                        if (consumedCount > 1)
                        {
                            pipeCtrl.MultiSlotDecodeAdvanceCount++;
                            AdvanceBundleSlotByConsumedCount(
                                issuePacket,
                                executableNonScalarPhysicalLaneMask);
                        }
                        else
                        {
                            AdvanceBundleSlot();
                        }
                    }
                    else
                    {
                        AdvanceBundleSlot();
                    }

                    return DecodeStageResult.Issued(currentBundlePc, issuedSlots);
                }

                return DecodeStageResult.NoProgress(
                    pipeIF.PC,
                    DecodeStageDiagnosticsKind.NoInput);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ResetDecodeAdmissionStateForBundle(ulong bundlePc)
            {
                pipeIDClusterPreparation = Core.ClusterIssuePreparation.CreateEmpty(bundlePc);
                pipeIDAdmissionPreparation = Core.RuntimeClusterAdmissionPreparation.CreateEmpty();
                pipeIDAdmissionCandidateView = Core.RuntimeClusterAdmissionCandidateView.CreateEmpty(bundlePc);
                pipeIDAdmissionDecisionDraft = Core.RuntimeClusterAdmissionDecisionDraft.CreateEmpty(bundlePc);
                pipeIDAdmissionHandoff = Core.RuntimeClusterAdmissionHandoff.CreateEmpty(bundlePc);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryResolveDecodeLocalStall(
                in Core.DecodedBundleSlotDescriptor currentSlotDescriptor,
                ulong currentBundlePc,
                out DecodeStageResult decodeStageResult)
            {
                if (currentSlotDescriptor.GetRuntimeAdmissionIsMemoryOp() &&
                    currentSlotDescriptor.GetRuntimeExecutionMemoryBankIntent() >= 0 &&
                    _fspScheduler != null)
                {
                    int bankId = currentSlotDescriptor.GetRuntimeExecutionMemoryBankIntent();
                    int vtId = currentSlotDescriptor.GetRuntimeExecutionVirtualThreadId();
                    if (_fspScheduler.IsBankPendingForVT(bankId, vtId))
                    {
                        decodeStageResult = DecodeStageResult.Stall(
                            currentBundlePc,
                            PipelineStallKind.MemoryWait,
                            bankConflict: true,
                            rejectedSlots: 1,
                            diagnostics: DecodeStageDiagnosticsKind.BankPending);
                        return true;
                    }
                }

                decodeStageResult = default;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentFspDerivedIssuePlan(
                ulong pc,
                System.Collections.Generic.IReadOnlyList<Core.MicroOp?> carrierBundle)
            {
                if (carrierBundle == null || carrierBundle.Count != 8)
                    return;

                Core.DecodedBundleTransportFacts transportFacts =
                    BuildDecodedBundleTransportFacts(
                        pc,
                        carrierBundle,
                        Core.DecodedBundleStateKind.ForegroundMutated,
                        Core.DecodedBundleStateOrigin.FspPacking);
                ReadCurrentDecodedBundleRuntimeState(
                    out Core.DecodedBundleRuntimeState runtimeState);
                PublishCurrentDerivedIssuePlanRuntimeState(
                    runtimeState.ApplyFspPacking(transportFacts));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentForegroundSlotMutation(
                byte slotIndex,
                Core.MicroOp microOp)
            {
                ReadCurrentExecutionDecodedBundleRuntimeState(
                    out Core.DecodedBundleRuntimeState currentExecutionRuntimeState);
                Core.DecodedBundleTransportFacts currentExecutionTransportFacts =
                    currentExecutionRuntimeState.TransportFacts;

                if (slotIndex >= 8 || slotIndex >= currentExecutionTransportFacts.Slots.Length)
                    return;

                Core.DecodedBundleTransportFacts mutatedTransportFacts =
                    Core.DecodedBundleSlotCarrierBuilder.BuildTransportFactsFromSingleSlotMutationContour(
                        currentExecutionTransportFacts.PC,
                        currentExecutionTransportFacts.Slots,
                        slotIndex,
                        microOp);
                if (decodedBundleDerivedIssuePlanState.MatchesBundlePc(currentExecutionTransportFacts.PC))
                {
                    PublishCurrentDerivedIssuePlanRuntimeState(
                        currentExecutionRuntimeState.ApplySingleSlotMutation(mutatedTransportFacts),
                        preserveCurrentProgress: true);
                    return;
                }

                PublishCurrentDecodedBundleRuntimeState(
                    currentExecutionRuntimeState.ApplySingleSlotMutation(mutatedTransportFacts),
                    preserveCurrentProgress: true);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ConsumeForegroundBundleSlotIfNonEmpty(
                byte slotIndex)
            {
                ConsumeDecodedBundleProgressSlotIfNonEmpty(slotIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ConsumeForegroundBundleIssuePacketSlots(
                Core.BundleIssuePacket issuePacket,
                byte executableNonScalarPhysicalLaneMask)
            {
                byte consumedSlotMask = ResolveConsumedDecodedBundleSlotMask(
                    issuePacket,
                    executableNonScalarPhysicalLaneMask);
                ConsumeDecodedBundleProgressMask(
                    consumedSlotMask,
                    pipelineBundleSlot);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.MicroOp?[] CloneDecodedBundleCarrierBundle(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> slotDescriptorsSource)
            {
                var carrierBundle = new Core.MicroOp?[8];
                for (byte slotIndex = 0; slotIndex < carrierBundle.Length; slotIndex++)
                {
                    carrierBundle[slotIndex] =
                        slotDescriptorsSource != null && slotIndex < slotDescriptorsSource.Count
                            ? slotDescriptorsSource[slotIndex].MicroOp
                            : null;
                }

                return carrierBundle;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceBundleSlot()
            {
                byte consumedSlotIndex = pipelineBundleSlot;
                ConsumeForegroundBundleSlotIfNonEmpty(consumedSlotIndex);

                if (TryGetNextRemainingBundleSlot(consumedSlotIndex, out byte nextSlotIndex))
                {
                    pipelineBundleSlot = nextSlotIndex;
                    return;
                }

                pipelineBundleSlot = 8;
                pipeIF.Valid = false;
                AdvanceActiveLivePc(256);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolveConsumedSlotCount(
                Core.BundleIssuePacket issuePacket,
                byte executableNonScalarPhysicalLaneMask)
            {
                return issuePacket.PreparedScalarLaneCount +
                    BitOperations.PopCount((uint)executableNonScalarPhysicalLaneMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceBundleSlotByConsumedCount(
                Core.BundleIssuePacket issuePacket,
                byte executableNonScalarPhysicalLaneMask)
            {
                byte currentSlotIndex = pipelineBundleSlot;
                ConsumeForegroundBundleIssuePacketSlots(
                    issuePacket,
                    executableNonScalarPhysicalLaneMask);

                if (TryGetNextRemainingBundleSlot(currentSlotIndex, out byte nextSlotIndex))
                {
                    pipelineBundleSlot = nextSlotIndex;
                    return;
                }

                pipelineBundleSlot = 8;
                pipeIF.Valid = false;
                AdvanceActiveLivePc(256);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryGetNextRemainingBundleSlot(byte currentSlotIndex, out byte slotIndex)
            {
                slotIndex = 0;

                ReadCurrentExecutionDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts transportFacts);
                Core.BundleProgressState progressState =
                    EnsureDecodedBundleProgressStateForTransportFacts(transportFacts);

                if (progressState.HasRemaining)
                {
                    slotIndex = progressState.NextSlotIndex;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.BundleProgressState EnsureDecodedBundleProgressStateForTransportFacts(
                in Core.DecodedBundleTransportFacts transportFacts)
            {
                Core.BundleProgressState progressState = decodedBundleProgressState;
                if (progressState.BundlePc != transportFacts.PC)
                {
                    progressState = Core.BundleProgressState.CreateForCursor(
                        transportFacts.PC,
                        transportFacts.ValidNonEmptyMask,
                        pipelineBundleSlot);
                    decodedBundleProgressState = progressState;
                }

                return progressState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ConsumeDecodedBundleProgressSlotIfNonEmpty(byte slotIndex)
            {
                ReadCurrentForegroundDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts transportFacts);
                if (slotIndex >= 8 || slotIndex >= transportFacts.Slots.Length)
                    return;

                Core.DecodedBundleSlotDescriptor slotDescriptor = transportFacts.Slots[slotIndex];
                if (!slotDescriptor.IsValid || slotDescriptor.GetRuntimeExecutionIsEmptyOrNop())
                    return;

                ConsumeDecodedBundleProgressMask(
                    (byte)(1 << slotIndex),
                    slotIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ConsumeDecodedBundleProgressMask(
                byte consumedSlotMask,
                byte currentSlotIndex)
            {
                if (consumedSlotMask == 0)
                    return;

                ReadCurrentExecutionDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts transportFacts);
                Core.BundleProgressState progressState =
                    EnsureDecodedBundleProgressStateForTransportFacts(transportFacts);
                decodedBundleProgressState = progressState.ConsumeMask(
                    consumedSlotMask,
                    currentSlotIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private byte ResolveConsumedDecodedBundleSlotMask(
                Core.BundleIssuePacket issuePacket,
                byte executableNonScalarPhysicalLaneMask)
            {
                ReadCurrentForegroundDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts transportFacts);
                byte consumedSlotMask = 0;

                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    if (!ShouldMaterializeIssuePacketLane(issuePacket, laneIndex, executableNonScalarPhysicalLaneMask))
                        continue;

                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (!lane.IsOccupied || lane.SlotIndex >= 8 || lane.SlotIndex >= transportFacts.Slots.Length)
                        continue;

                    Core.DecodedBundleSlotDescriptor slotDescriptor = transportFacts.Slots[lane.SlotIndex];
                    if (!slotDescriptor.IsValid || slotDescriptor.GetRuntimeExecutionIsEmptyOrNop())
                        continue;

                    consumedSlotMask |= (byte)(1 << lane.SlotIndex);
                }

                return consumedSlotMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordClusterPathProbeDiagnostics(
                Core.RuntimeClusterAdmissionCandidateView candidateView,
                Core.RuntimeClusterAdmissionDecisionDraft decisionDraft)
            {
                if (!decisionDraft.ShouldProbeClusterPath)
                    return;

                pipeCtrl.ClusterProbeCount++;
                pipeCtrl.ClusterProbeRefinedWidthSum += (ulong)candidateView.RefinedAdvisoryScalarIssueWidth;

                if (candidateView.SuggestsFallbackDiagnostics)
                {
                    pipeCtrl.ClusterProbeNarrowFallbackCount++;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordDifferentialTraceEntry(
                Core.RuntimeClusterAdmissionCandidateView candidateView,
                Core.RuntimeClusterAdmissionDecisionDraft decisionDraft)
            {
                if (differentialTraceCapture == null)
                    return;

                differentialTraceCapture.AddEntry(
                    Core.DifferentialTraceEntry.FromAdvisoryChain(candidateView, decisionDraft));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong ResolveCurrentLoopBufferMaxIterations(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> currentBundleSlots)
            {
                if (pipeIF.VLIWBundle == null)
                    return 0;

                if (currentBundleSlots == null || currentBundleSlots.Count == 0)
                    return 0;

                ulong vlmax = RVV_Config.VLMAX;
                if (vlmax == 0)
                    return 0;

                ulong maxIterations = 0;
                for (int slotIndex = 0; slotIndex < currentBundleSlots.Count; slotIndex++)
                {
                    Core.DecodedBundleSlotDescriptor currentSlot = currentBundleSlots[slotIndex];
                    if (!currentSlot.IsValid || currentSlot.GetRuntimeExecutionIsEmptyOrNop() || !currentSlot.IsVectorOp)
                        continue;

                    if (!TryReadCurrentFetchSlotInstruction(slotIndex, out VLIW_Instruction rawInstruction))
                        continue;

                    if (rawInstruction.StreamLength <= 1)
                        continue;

                    ulong iterations = ((ulong)rawInstruction.StreamLength + vlmax - 1) / vlmax;
                    if (iterations > maxIterations)
                        maxIterations = iterations;
                }

                return maxIterations;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryReadCurrentFetchSlotInstruction(
                int slotIndex,
                out VLIW_Instruction instruction)
            {
                instruction = default;

                if (pipeIF.VLIWBundle == null || (uint)slotIndex >= Core.BundleMetadata.BundleSlotCount)
                    return false;

                return TryReadFetchedBundleInstructionUnchecked(
                    pipeIF.VLIWBundle,
                    slotIndex,
                    out instruction);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordExecutableClusterAdmissionChoice(Core.RuntimeClusterAdmissionDecisionDraft decisionDraft)
            {
                if (!pipeCtrl.ClusterPreparedModeEnabled)
                {
                    if (decisionDraft.ExecutionMode is Core.RuntimeClusterAdmissionExecutionMode.ReferenceSequential
                        or Core.RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference)
                    {
                        pipeCtrl.WidePathGate3_ReferenceSequentialCount++;
                    }

                    return;
                }

                switch (decisionDraft.ExecutionMode)
                {
                    case Core.RuntimeClusterAdmissionExecutionMode.ClusterPrepared:
                        pipeCtrl.ClusterPreparedExecutionChoiceCount++;
                        pipeCtrl.WidePathSuccessCount++;
                        break;
                    case Core.RuntimeClusterAdmissionExecutionMode.ClusterPreparedRefined:
                        pipeCtrl.ClusterPreparedExecutionChoiceCount++;
                        pipeCtrl.WidePathSuccessCount++;
                        pipeCtrl.RefinedMaskPromotionCount++;
                        break;
                    case Core.RuntimeClusterAdmissionExecutionMode.ReferenceSequentialFallback:
                        pipeCtrl.ReferenceSequentialFallbackCount++;
                        pipeCtrl.ClusterModeFallbackCount++;
                        if (decisionDraft.ScalarIssueMask == 0)
                            pipeCtrl.WidePathGate6_PreparedMaskZeroCount++;
                        else
                            pipeCtrl.WidePathGate4_NarrowFallbackCount++;
                        break;
                    case Core.RuntimeClusterAdmissionExecutionMode.ReferenceSequential:
                        pipeCtrl.WidePathGate5_NotClusterCandidateCount++;
                        break;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordDecoderModernizationTelemetry(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> bundleSlots,
                Core.ClusterIssuePreparation clusterPreparation,
                Core.RuntimeClusterAdmissionCandidateView candidateView)
            {
                if (HasMemoryClusteringEvent(bundleSlots))
                {
                    _fspScheduler?.RecordMemoryClusteringEvent();
                }

                byte preparedMask = candidateView.PreparedScalarMask;
                int preparedCount = System.Numerics.BitOperations.PopCount((uint)preparedMask);

                if (preparedCount >= 2)
                {
                    pipeCtrl.DecoderPreparedScalarGroupCount++;

                    if (candidateView.SuggestsFallbackDiagnostics)
                    {
                        pipeCtrl.DecoderPreparedFallbackCount++;
                    }

                    if (clusterPreparation.ScalarClusterGroup.Count > 0)
                    {
                        Core.VtScalarCandidateSummary vtSummary = clusterPreparation.ScalarClusterGroup.BuildVtSummary();
                        pipeCtrl.VTSpreadPerBundle += (ulong)vtSummary.ActiveVtCount;
                    }
                }

                byte blockedMask = candidateView.BlockedScalarCandidateMask;
                pipeCtrl.ScalarClusterEligibleButBlockedCount += (ulong)System.Numerics.BitOperations.PopCount((uint)blockedMask);

                if (candidateView.ScalarCandidateMask != 0)
                {
                    byte flatBlockedMask = (byte)(candidateView.RegisterHazardMask | candidateView.OrderingHazardMask);
                    byte flatWideReady = (byte)(candidateView.ScalarCandidateMask & ~flatBlockedMask);
                    byte actualWideReady = clusterPreparation.AdmissionPrep.WideReadyScalarMask;
                    if (System.Numerics.BitOperations.PopCount((uint)actualWideReady) >
                        System.Numerics.BitOperations.PopCount((uint)flatWideReady))
                    {
                        pipeCtrl.FallbackSofteningPromotionCount++;
                    }
                }

                Core.DecodedBundleDependencySummary? depSummary = clusterPreparation.DependencySummary;
                if (depSummary.HasValue)
                {
                    byte scalarEligibleMask = clusterPreparation.AdmissionPrep.ScalarCandidateMask;
                    for (int slot = 0; slot < 8; slot++)
                    {
                        byte slotBit = (byte)(1 << slot);
                        for (int peer = slot + 1; peer < 8; peer++)
                        {
                            byte peerBit = (byte)(1 << peer);
                            Core.HazardTriageClass triage = depSummary.Value.QueryPairHazard(
                                (byte)slot,
                                (byte)peer,
                                scalarEligibleMask,
                                out Core.HazardEffectKind effectKind);

                            if (triage == Core.HazardTriageClass.Safe)
                                continue;

                            switch (effectKind)
                            {
                                case Core.HazardEffectKind.RegisterData:
                                    pipeCtrl.HazardRegisterDataCount++;
                                    break;
                                case Core.HazardEffectKind.MemoryBank:
                                    pipeCtrl.HazardMemoryBankCount++;
                                    break;
                                case Core.HazardEffectKind.ControlFlow:
                                    pipeCtrl.HazardControlFlowCount++;
                                    break;
                                case Core.HazardEffectKind.SystemBarrier:
                                    pipeCtrl.HazardSystemBarrierCount++;
                                    break;
                                case Core.HazardEffectKind.PinnedLane:
                                    pipeCtrl.HazardPinnedLaneCount++;
                                    break;
                            }

                            bool bothScalarEligible = (scalarEligibleMask & slotBit) != 0 && (scalarEligibleMask & peerBit) != 0;
                            if (bothScalarEligible && triage == Core.HazardTriageClass.HardReject)
                            {
                                pipeCtrl.CrossSlotRejectCount++;
                            }
                        }
                    }

                    if (candidateView.SuggestsFallbackDiagnostics)
                    {
                        if (depSummary.Value.ControlConflictMask != 0)
                        {
                            pipeCtrl.ReferenceFallbackDueToControlConflictCount++;
                        }

                        if (depSummary.Value.MemoryConflictMask != 0)
                        {
                            pipeCtrl.ReferenceFallbackDueToMemoryConflictCount++;
                        }
                    }
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasMemoryClusteringEvent(
                System.Collections.Generic.IReadOnlyList<Core.DecodedBundleSlotDescriptor> slots)
            {
                int slotCount = Math.Min(8, slots.Count);
                for (int i = 0; i < slotCount; i++)
                {
                    if (!slots[i].GetRuntimeAdmissionIsMemoryOp() ||
                        slots[i].GetRuntimeExecutionMemoryBankIntent() < 0)
                        continue;

                    int bankIntent = slots[i].GetRuntimeExecutionMemoryBankIntent();
                    for (int j = i + 1; j < slotCount; j++)
                    {
                        if (slots[j].GetRuntimeAdmissionIsMemoryOp() &&
                            slots[j].GetRuntimeExecutionMemoryBankIntent() == bankIntent)
                            return true;
                    }
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsExecutableNonScalarIssueLane(Core.IssuePacketLane issueLane)
            {
                if (!issueLane.IsOccupied || !issueLane.IsNonScalarSelection)
                    return false;

                if (issueLane.MicroOp is Core.TrapMicroOp)
                {
                    return issueLane.PhysicalLaneIndex is >= 4 and < 8;
                }

                return issueLane.PhysicalLaneIndex switch
                {
                    4 or 5 => issueLane.MicroOp is Core.LoadStoreMicroOp || issueLane.MicroOp?.IsAssist == true,
                    7 => IsExecutableSystemSingletonIssueLaneMicroOp(issueLane.MicroOp),
                    _ => false
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsExecutableSystemSingletonIssueLaneMicroOp(Core.MicroOp? microOp)
            {
                // Lane 7 widened execution must admit every current authoritative singleton
                // carrier that already has explicit mainline execute/retire follow-through or
                // issue-packet fail-closed surface validation. Canonical BranchMicroOp now owns
                // the shared conditional/unconditional branch execution payload that later
                // retires through the WB-authoritative control-flow contour, so lane 7 must
                // not filter it by a legacy conditional-only shape.
                // Dead placeholders such as Halt/PortIO remain excluded until they gain a
                // production-reachable contour.
                return microOp is Core.BranchMicroOp
                    || microOp is Core.SysEventMicroOp
                    || microOp is Core.StreamControlMicroOp
                    || microOp is Core.CSRMicroOp
                    || microOp is Core.VmxMicroOp
                    || microOp is Core.VConfigMicroOp;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasIssueLaneHazardAgainstSlotMask(
                in Core.DecodedBundleDependencySummary dependencySummary,
                byte slotIndex,
                byte peerSlotMask,
                byte scalarGroupMask)
            {
                for (byte peerSlotIndex = 0; peerSlotIndex < 8; peerSlotIndex++)
                {
                    if (peerSlotIndex == slotIndex)
                        continue;

                    byte peerBit = (byte)(1 << peerSlotIndex);
                    if ((peerSlotMask & peerBit) == 0)
                        continue;

                    if (dependencySummary.QueryPairHazard(
                        slotIndex,
                        peerSlotIndex,
                        scalarGroupMask,
                        out _) != Core.HazardTriageClass.Safe)
                    {
                        return true;
                    }
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveExecutableNonScalarPhysicalLaneMask(
                Core.BundleIssuePacket issuePacket,
                Core.DecodedBundleDependencySummary? dependencySummary)
            {
                if (!dependencySummary.HasValue)
                    return 0;

                byte executablePhysicalLaneMask = 0;
                byte acceptedSlotMask = 0;
                Core.DecodedBundleDependencySummary summary = dependencySummary.Value;

                for (byte laneIndex = 4; laneIndex < 8; laneIndex++)
                {
                    Core.IssuePacketLane issueLane = issuePacket.GetPhysicalLane(laneIndex);
                    if (!IsExecutableNonScalarIssueLane(issueLane) || issueLane.SlotIndex >= 8)
                        continue;

                    if (HasIssueLaneHazardAgainstSlotMask(
                        summary,
                        issueLane.SlotIndex,
                        issuePacket.ScalarIssueMask,
                        issuePacket.ScalarIssueMask))
                    {
                        continue;
                    }

                    if (HasIssueLaneHazardAgainstSlotMask(
                        summary,
                        issueLane.SlotIndex,
                        acceptedSlotMask,
                        issuePacket.ScalarIssueMask))
                    {
                        continue;
                    }

                    executablePhysicalLaneMask |= (byte)(1 << laneIndex);
                    acceptedSlotMask |= (byte)(1 << issueLane.SlotIndex);
                }

                return executablePhysicalLaneMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveExecutableNonScalarSlotMask(
                Core.BundleIssuePacket issuePacket,
                byte executableNonScalarPhysicalLaneMask)
            {
                byte slotMask = 0;

                for (byte laneIndex = 4; laneIndex < 8; laneIndex++)
                {
                    if ((executableNonScalarPhysicalLaneMask & (1 << laneIndex)) == 0)
                        continue;

                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (lane.IsOccupied && lane.SlotIndex < 8)
                    {
                        slotMask |= (byte)(1 << lane.SlotIndex);
                    }
                }

                return slotMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CurrentDecodedBundleHasDecodeFault(ulong currentBundlePc)
            {
                ReadCurrentExecutionDecodedBundleRuntimeState(
                    out Core.DecodedBundleRuntimeState runtimeState);
                return runtimeState.BundlePc == currentBundlePc &&
                    runtimeState.HasDecodeFault;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.DecodedBundleSlotDescriptor[] BuildForegroundExecutionSlotView(
                ulong currentBundlePc,
                Core.DecodedBundleSlotDescriptor[] currentBundleSlots)
            {
                if (!CurrentDecodedBundleHasDecodeFault(currentBundlePc))
                {
                    return currentBundleSlots;
                }

                var filteredSlots = new Core.DecodedBundleSlotDescriptor[currentBundleSlots.Length];
                for (byte slotIndex = 0; slotIndex < filteredSlots.Length; slotIndex++)
                {
                    Core.DecodedBundleSlotDescriptor slot = currentBundleSlots[slotIndex];
                    filteredSlots[slotIndex] = slot.IsValid && slot.MicroOp is Core.TrapMicroOp
                        ? slot
                        : Core.DecodedBundleSlotDescriptor.Create(slotIndex, null);
                }

                return filteredSlots;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void AlignForegroundExecutionAdmissionFacts(
                ulong currentBundlePc,
                Core.DecodedBundleSlotDescriptor[] currentBundleSlots,
                Core.DecodedBundleSlotDescriptor[] executionBundleSlots,
                ref Core.DecodedBundleAdmissionPrep admissionPrep,
                ref Core.DecodedBundleDependencySummary? dependencySummary)
            {
                if (ReferenceEquals(currentBundleSlots, executionBundleSlots))
                    return;

                Core.DecodedBundleTransportFacts executionTransportFacts =
                    Core.DecodedBundleSlotCarrierBuilder.BuildTransportFactsFromSlotDescriptorProjection(
                        currentBundlePc,
                        executionBundleSlots,
                        Core.DecodedBundleStateKind.ForegroundMutated,
                        Core.DecodedBundleStateOrigin.ClearMaskMutation);
                admissionPrep = executionTransportFacts.AdmissionPrep;
                dependencySummary = executionTransportFacts.DependencySummary;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ConsumeDecodeStateAfterExecuteDispatch()
            {
                pipeID.Valid = false;
                pipeIDClusterPreparation = Core.ClusterIssuePreparation.CreateEmpty(pipeID.PC);
                pipeIDAdmissionPreparation = Core.RuntimeClusterAdmissionPreparation.CreateEmpty();
                pipeIDAdmissionCandidateView = Core.RuntimeClusterAdmissionCandidateView.CreateEmpty(pipeID.PC);
                pipeIDAdmissionDecisionDraft = Core.RuntimeClusterAdmissionDecisionDraft.CreateEmpty(pipeID.PC);
                pipeIDAdmissionHandoff = Core.RuntimeClusterAdmissionHandoff.CreateEmpty(pipeID.PC);
            }
        }
    }
}
