using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03FetchedOwnerThreadTailTests
{
    private const ulong MemoryDomainMaskLow = 0xFFFFUL << 32;

    [Fact]
    public void DecodeFullBundle_CanonicalSuccessPath_UsesFetchedAnnotationCarrierInsteadOfActiveVirtualThread()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4B00, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
        core.WriteVirtualThreadPipelineState(2, PipelineState.Task);
        Assert.Equal(0, core.ReadActiveVirtualThreadId());

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));
        VliwBundleAnnotations annotations = CreateUniformOwnerAnnotations(2);

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4B00, annotations: annotations);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        var runtimeFacts = core.TestReadDecodedSlotRuntimeIssueFacts(transportFacts.Slots[0]);
        var canonicalSlot = canonicalBundle.GetDecodedSlot(0);

        Assert.False(canonicalBundle.HasDecodeFault);
        Assert.True(canonicalSlot.IsOccupied);
        Assert.Equal((uint)InstructionsEnum.ADDI, transportFacts.Slots[0].OpCode);
        Assert.Equal(2, (int)canonicalSlot.SlotMetadata.VirtualThreadId.Value);
        Assert.Equal(2, runtimeFacts.VirtualThreadId);
        Assert.Equal(2, transportFacts.Slots[0].OwnerThreadId);
        Assert.NotNull(transportFacts.Slots[0].MicroOp);
        Assert.Equal(2, transportFacts.Slots[0].MicroOp!.VirtualThreadId);
        Assert.Equal(2, transportFacts.Slots[0].MicroOp!.OwnerThreadId);
        Assert.Equal(0, transportFacts.Slots[0].MicroOp!.OwnerContextId);
        Assert.Equal(0, transportFacts.Slots[0].MicroOp!.AdmissionMetadata.OwnerContextId);
    }

    [Fact]
    public void DecodeFullBundle_CanonicalLeadingGap_UsesFetchedAnnotationCarrierForMaterializedNopSlot()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4B10, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
        core.WriteVirtualThreadPipelineState(2, PipelineState.Task);
        Assert.Equal(0, core.ReadActiveVirtualThreadId());

        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[1] = CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4);
        VliwBundleAnnotations annotations = CreateUniformOwnerAnnotations(2);

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4B10, annotations: annotations);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        var leadingGap = Assert.IsType<NopMicroOp>(transportFacts.Slots[0].MicroOp);
        var occupiedSlot = transportFacts.Slots[1];

        Assert.True(transportFacts.Slots[0].IsEmptyOrNop);
        Assert.Equal(2, leadingGap.VirtualThreadId);
        Assert.Equal(2, leadingGap.OwnerThreadId);
        Assert.Equal(0, leadingGap.OwnerContextId);
        Assert.Equal(0, leadingGap.AdmissionMetadata.OwnerContextId);
        Assert.NotNull(occupiedSlot.MicroOp);
        Assert.Equal(2, occupiedSlot.VirtualThreadId);
        Assert.Equal(2, occupiedSlot.OwnerThreadId);
        Assert.Equal(2, occupiedSlot.MicroOp!.VirtualThreadId);
        Assert.Equal(2, occupiedSlot.MicroOp!.OwnerThreadId);
        Assert.Equal(0, occupiedSlot.MicroOp!.OwnerContextId);
        Assert.Equal(0, occupiedSlot.MicroOp!.AdmissionMetadata.OwnerContextId);
    }

    [Fact]
    public void DecodeFullBundle_FallbackTrapPath_UsesFetchedAnnotationCarrierInsteadOfActiveVirtualThread()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4C00, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
        core.WriteVirtualThreadPipelineState(3, PipelineState.Task);
        Assert.Equal(0, core.ReadActiveVirtualThreadId());

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction((InstructionsEnum)14),
                CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));
        VliwBundleAnnotations annotations = CreateUniformOwnerAnnotations(3);

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4C00, annotations: annotations);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(transportFacts.Slots[0].MicroOp);
        AtomicMicroOp atomicMicroOp = Assert.IsType<AtomicMicroOp>(transportFacts.Slots[1].MicroOp);

        Assert.Equal(3, trapMicroOp.VirtualThreadId);
        Assert.Equal(3, trapMicroOp.OwnerThreadId);
        Assert.Equal(0, trapMicroOp.OwnerContextId);
        Assert.Equal(0, trapMicroOp.AdmissionMetadata.OwnerContextId);
        Assert.Equal(3, atomicMicroOp.VirtualThreadId);
        Assert.Equal(3, atomicMicroOp.OwnerThreadId);
        Assert.Equal(0, atomicMicroOp.OwnerContextId);
        Assert.Equal(0, atomicMicroOp.AdmissionMetadata.OwnerContextId);
    }

    [Fact]
    public void DecodeFullBundle_CanonicalAtomicSuccessPath_UsesFetchedAnnotationCarrierForOwnerResourceMask()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4C10, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
        core.WriteVirtualThreadPipelineState(3, PipelineState.Task);
        Assert.Equal(0, core.ReadActiveVirtualThreadId());

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));
        VliwBundleAnnotations annotations = CreateUniformOwnerAnnotations(3);

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4C10, annotations: annotations);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        AtomicMicroOp atomicMicroOp = Assert.IsType<AtomicMicroOp>(transportFacts.Slots[0].MicroOp);

        Assert.Equal(3, atomicMicroOp.VirtualThreadId);
        Assert.Equal(3, atomicMicroOp.OwnerThreadId);
        Assert.Equal(
            ResourceMaskBuilder.ForMemoryDomain(3).Low,
            atomicMicroOp.ResourceMask.Low & MemoryDomainMaskLow);
        Assert.Equal(
            0UL,
            atomicMicroOp.ResourceMask.Low & ResourceMaskBuilder.ForMemoryDomain(0).Low);
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenBundleOwnerArrivesViaFetchedAnnotations_ThenAssistOwnerSnapshotUsesFetchedBundleOwner()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D00, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
            core.WriteVirtualThreadPipelineState(2, PipelineState.Task);
            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));
            VliwBundleAnnotations annotations = CreateUniformOwnerAnnotations(2);

            core.TestDecodeFetchedBundle(rawSlots, pc: 0x4D00, annotations: annotations);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            Assert.True(pod.TryGetAssistOwnerSnapshot(0, out PodController.AssistOwnerSnapshot snapshot));
            Assert.Equal(2, snapshot.VirtualThreadId);
            Assert.Equal(0, snapshot.OwnerContextId);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void DecodeStage_WithFspPackingEnabled_WhenBundleOwnerArrivesViaFetchedAnnotations_ThenAssistOwnerSnapshotUsesFetchedBundleOwner()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D08, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
            core.WriteVirtualThreadPipelineState(2, PipelineState.Task);

            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4));
            VliwBundleAnnotations annotations = CreateUniformOwnerAnnotations(2);

            core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc: 0x4D08, annotations: annotations);
            var decodeStage = core.TestReadDecodeStageStatus();

            Assert.True(decodeStage.Valid);
            Assert.True(pod.TryGetAssistOwnerSnapshot(0, out PodController.AssistOwnerSnapshot snapshot));
            Assert.Equal(2, snapshot.VirtualThreadId);
            Assert.Equal(0, snapshot.OwnerContextId);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void ReplayFetch_WhenActiveThreadDiffers_ThenForegroundTransportUsesReplayCarrierOwner()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4D18, activeVtId: 0);
        core.ActiveVirtualThreadId = 0;
        core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
        core.WriteVirtualThreadPipelineState(3, PipelineState.Task);

        MicroOp replayCarrier = MicroOpTestHelper.CreateScalarALU(3, destReg: 4, src1Reg: 5, src2Reg: 6);
        core.TestPrimeReplayPhase(0x4D18, totalIterations: 2, replayCarrier);

        core.ExecutePipelineCycle();

        Processor.CPU_Core.FetchStage fetchStage = core.GetFetchStage();
        Assert.True(fetchStage.Valid);
        Assert.Null(fetchStage.VLIWBundle);
        Assert.False(fetchStage.HasBundleAnnotations);
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();

        Assert.Equal(DecodedBundleStateKind.Replay, transportFacts.StateKind);
        Assert.Equal(DecodedBundleStateOrigin.ReplayBundleLoad, transportFacts.StateOrigin);
        Assert.NotNull(transportFacts.Slots[0].MicroOp);
        Assert.Equal(3, transportFacts.Slots[0].MicroOp!.VirtualThreadId);
        Assert.Equal(3, transportFacts.Slots[0].MicroOp!.OwnerThreadId);
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenLeadingNopPrecedesBundleOwner_ThenAssistOwnerSnapshotUsesFirstLiveNonNopOwner()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D10, activeVtId: 2);
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteVirtualThreadPipelineState(0, PipelineState.WaitForEvent);
            core.WriteVirtualThreadPipelineState(2, PipelineState.Task);
            VLIW_Instruction[] rawSlots =
                CreateBundle(
                    default,
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4),
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 8),
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 5, rs1: 6, immediate: 12),
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 7, rs1: 8, immediate: 16),
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 9, rs1: 10, immediate: 20),
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 11, rs1: 12, immediate: 24),
                    CreateScalarInstruction(InstructionsEnum.ADDI, rd: 13, rs1: 14, immediate: 28));

            core.TestDecodeFetchedBundle(rawSlots, pc: 0x4D10);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            Assert.True(pod.TryGetAssistOwnerSnapshot(0, out PodController.AssistOwnerSnapshot snapshot));
            Assert.Equal(2, snapshot.VirtualThreadId);
            Assert.Equal(0, snapshot.OwnerContextId);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenCanonicalBundleHasFspBoundary_ThenInterCoreInjectionIsSkipped()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D20);
            core.VectorConfig.FSP_Enabled = 1;

            var rawSlots = new[]
            {
                default(VLIW_Instruction),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 8),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 5, rs1: 6, immediate: 12),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 7, rs1: 8, immediate: 16),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 9, rs1: 10, immediate: 20),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 11, rs1: 12, immediate: 24),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 13, rs1: 14, immediate: 28)
            };
            var annotations = new VliwBundleAnnotations(
                new InstructionSlotMetadata[0],
                new BundleMetadata { FspBoundary = true });
            MicroOp candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, annotations, pc: 0x4D20, bundleSerial: 75);
            scheduler.Nominate(1, candidate);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentForegroundDecodedBundleTransportFacts();

            Assert.True(transportFacts.Slots[0].IsEmptyOrNop);
            Assert.IsType<NopMicroOp>(transportFacts.Slots[0].MicroOp);
            Assert.Equal((uint)InstructionsEnum.ADDI, transportFacts.Slots[1].OpCode);
            Assert.False(candidate.IsFspInjected);
            Assert.True(scheduler.HasNominatedCandidate(1));
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenCanonicalGapIsMarkedNotStealable_ThenInterCoreInjectionIsSkipped()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D28);
            core.VectorConfig.FSP_Enabled = 1;

            var rawSlots = new[]
            {
                default(VLIW_Instruction),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 8),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 5, rs1: 6, immediate: 12),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 7, rs1: 8, immediate: 16),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 9, rs1: 10, immediate: 20),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 11, rs1: 12, immediate: 24),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 13, rs1: 14, immediate: 28)
            };
            var annotations = new VliwBundleAnnotations(
                new[]
                {
                    new InstructionSlotMetadata(
                        YAKSys_Hybrid_CPU.Core.Registers.VtId.Create(0),
                        new SlotMetadata
                        {
                            StealabilityPolicy = StealabilityPolicy.NotStealable
                        })
                });
            MicroOp candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, annotations, pc: 0x4D28, bundleSerial: 77);
            scheduler.Nominate(1, candidate);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentForegroundDecodedBundleTransportFacts();
            NopMicroOp preservedGap = Assert.IsType<NopMicroOp>(transportFacts.Slots[0].MicroOp);

            Assert.True(transportFacts.Slots[0].IsEmptyOrNop);
            Assert.False(preservedGap.AdmissionMetadata.IsStealable);
            Assert.Equal((uint)InstructionsEnum.ADDI, transportFacts.Slots[1].OpCode);
            Assert.Equal((uint)InstructionsEnum.ADDI, transportFacts.Slots[7].OpCode);
            Assert.False(candidate.IsFspInjected);
            Assert.True(scheduler.HasNominatedCandidate(1));
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenCanonicalBundleHasNoFspBoundary_ThenInterCoreInjectionStillOccurs()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D30);
            core.VectorConfig.FSP_Enabled = 1;

            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[1] = CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4);
            var annotations = new VliwBundleAnnotations(
                new InstructionSlotMetadata[0],
                new BundleMetadata { FspBoundary = false });
            MicroOp candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, annotations, pc: 0x4D30, bundleSerial: 76);
            scheduler.Nominate(1, candidate);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentForegroundDecodedBundleTransportFacts();

            Assert.Same(candidate, transportFacts.Slots[0].MicroOp);
            Assert.Equal(1, transportFacts.Slots[0].VirtualThreadId);
            Assert.True(candidate.IsFspInjected);
            Assert.False(scheduler.HasNominatedCandidate(1));
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenSingleOwnerFullMemoryBundleFallsBack_ThenInterCoreAssistTransportIsNominated()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D38, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);

            VLIW_Instruction[] rawSlots =
            [
                CreateScalarInstruction(InstructionsEnum.LW, rd: 1, rs1: 2, immediate: 0x40),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 4),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 5, rs1: 6, immediate: 8),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 7, rs1: 8, immediate: 12),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 9, rs1: 10, immediate: 16),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 11, rs1: 12, immediate: 20),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 13, rs1: 14, immediate: 24),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 15, rs1: 16, immediate: 28)
            ];

            core.TestDecodeFetchedBundle(rawSlots, pc: 0x4D38, annotations: CreateUniformOwnerAnnotations(0));

            core.TestRefreshCurrentFspDerivedIssuePlan();

            Assert.True(pod.TryPeekInterCoreAssistTransport(0, out AssistInterCoreTransport transport));
            Assert.True(transport.IsValid);
            Assert.Equal(1, scheduler.AssistInterCoreNominations);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenForegroundBundleStillSpansMultipleVts_ThenInterCoreAssistFallbackIsSuppressed()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D40, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);
            core.WriteVirtualThreadPipelineState(1, PipelineState.Task);

            VLIW_Instruction[] rawSlots =
            [
                CreateScalarInstruction(InstructionsEnum.LW, rd: 1, rs1: 2, immediate: 0x40),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 4),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 5, rs1: 6, immediate: 8),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 7, rs1: 8, immediate: 12),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 9, rs1: 10, immediate: 16),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 11, rs1: 12, immediate: 20),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 13, rs1: 14, immediate: 24),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 15, rs1: 16, immediate: 28)
            ];

            var slotMetadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
            for (int slotIndex = 0; slotIndex < slotMetadata.Length; slotIndex++)
            {
                slotMetadata[slotIndex] = new InstructionSlotMetadata(
                    YAKSys_Hybrid_CPU.Core.Registers.VtId.Create(0),
                    SlotMetadata.Default);
            }

            slotMetadata[1] = new InstructionSlotMetadata(
                YAKSys_Hybrid_CPU.Core.Registers.VtId.Create(1),
                SlotMetadata.Default);

            core.TestDecodeFetchedBundle(
                rawSlots,
                pc: 0x4D40,
                annotations: new VliwBundleAnnotations(slotMetadata));

            core.TestRefreshCurrentFspDerivedIssuePlan();

            Assert.False(pod.TryPeekInterCoreAssistTransport(0, out _));
            Assert.Equal(0, scheduler.AssistInterCoreNominations);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenBundleContainsStreamWaitBoundary_ThenInterCoreAssistFallbackIsSuppressed()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler();
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D44, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);

            VLIW_Instruction[] rawSlots =
            [
                CreateScalarInstruction(InstructionsEnum.LW, rd: 1, rs1: 2, immediate: 0x40),
                CreateScalarInstruction(InstructionsEnum.STREAM_WAIT),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 4),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 5, rs1: 6, immediate: 8),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 7, rs1: 8, immediate: 12),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 9, rs1: 10, immediate: 16),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 11, rs1: 12, immediate: 20),
                CreateScalarInstruction(InstructionsEnum.ADDI, rd: 13, rs1: 14, immediate: 24)
            ];

            core.TestDecodeFetchedBundle(rawSlots, pc: 0x4D44, annotations: CreateUniformOwnerAnnotations(0));

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentForegroundDecodedBundleTransportFacts();

            Assert.False(pod.TryPeekInterCoreAssistTransport(0, out _));
            Assert.Equal(0, scheduler.AssistInterCoreNominations);
            Assert.Equal((uint)InstructionsEnum.STREAM_WAIT, transportFacts.Slots[1].OpCode);
            Assert.IsType<StreamControlMicroOp>(transportFacts.Slots[1].MicroOp);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    [Fact]
    public void RefreshCurrentFspDerivedIssuePlan_WhenHiddenForegroundSlotWouldBlockTypedSlotAdmission_ThenOwnerBundleStripsItBeforeSmtPacking()
    {
        PodController? originalPod = Processor.Pods[0];
        try
        {
            var scheduler = new MicroOpScheduler
            {
                TypedSlotEnabled = true
            };
            var pod = new PodController(0, 0, scheduler);
            Processor.Pods[0] = pod;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(0x4D48, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.VectorConfig.FSP_Enabled = 1;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);
            core.WriteVirtualThreadPipelineState(1, PipelineState.Task);
            core.WriteVirtualThreadPipelineState(3, PipelineState.WaitForEvent);

            ScalarALUMicroOp owner =
                CreateScalarAluWithSharedStructuralMask(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            ScalarALUMicroOp hiddenForegroundBlocker =
                CreateScalarAluWithSharedStructuralMask(3, destReg: 4, src1Reg: 5, src2Reg: 6, sharedLowMask: 1UL << 40);
            ScalarALUMicroOp candidate =
                CreateScalarAluWithSharedStructuralMask(1, destReg: 7, src1Reg: 8, src2Reg: 9, sharedLowMask: 1UL << 40);

            core.TestSetDecodedBundle(
                null,
                owner,
                hiddenForegroundBlocker,
                candidate,
                null,
                null,
                null,
                null);

            core.TestRefreshCurrentFspDerivedIssuePlan();

            DecodedBundleTransportFacts transportFacts = core.TestReadCurrentForegroundDecodedBundleTransportFacts();

            Assert.Same(candidate, transportFacts.Slots[0].MicroOp);
            Assert.Equal(1, transportFacts.Slots[0].VirtualThreadId);
            Assert.True(candidate.IsFspInjected);
            Assert.True(transportFacts.Slots[2].IsEmptyOrNop);
            Assert.True(transportFacts.Slots[3].IsEmptyOrNop);
            Assert.Equal(0, scheduler.TypedSlotResourceConflictRejects);
            Assert.Equal(0, scheduler.RejectionsVT1);
        }
        finally
        {
            Processor.Pods[0] = originalPod;
        }
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] occupiedSlots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < occupiedSlots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = occupiedSlots[slotIndex];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = virtualThreadId
        };
    }

    private static ScalarALUMicroOp CreateScalarAluWithSharedStructuralMask(
        int virtualThreadId,
        ushort destReg,
        ushort src1Reg,
        ushort src2Reg,
        ulong sharedLowMask = 0,
        ulong sharedHighMask = 0)
    {
        ScalarALUMicroOp microOp = MicroOpTestHelper.CreateScalarALU(
            virtualThreadId,
            destReg,
            src1Reg,
            src2Reg);

        if (sharedLowMask != 0 || sharedHighMask != 0)
        {
            microOp.SafetyMask = microOp.SafetyMask | new SafetyMask128(sharedLowMask, sharedHighMask);
            microOp.InitializeMetadata();
        }

        return microOp;
    }

    private static VliwBundleAnnotations CreateUniformOwnerAnnotations(
        byte ownerVirtualThreadId,
        BundleMetadata? bundleMetadata = null)
    {
        var slotMetadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        InstructionSlotMetadata ownerSlotMetadata = new(
            YAKSys_Hybrid_CPU.Core.Registers.VtId.Create(ownerVirtualThreadId),
            SlotMetadata.Default);
        for (int slotIndex = 0; slotIndex < slotMetadata.Length; slotIndex++)
        {
            slotMetadata[slotIndex] = ownerSlotMetadata;
        }

        return bundleMetadata == null
            ? new VliwBundleAnnotations(slotMetadata)
            : new VliwBundleAnnotations(slotMetadata, bundleMetadata);
    }
}

