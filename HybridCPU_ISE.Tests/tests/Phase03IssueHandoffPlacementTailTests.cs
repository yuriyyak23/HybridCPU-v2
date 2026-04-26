using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03IssueHandoffPlacementTailTests
{
    [Fact]
    public void AdmissionIssueHandoff_UsesAdmissionPlacement_WhenCarrierPlacementIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x1000);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.STREAM_WAIT));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x1000, bundleSerial: 61);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        StreamControlMicroOp microOp = Assert.IsType<StreamControlMicroOp>(canonicalSlot.MicroOp);

        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithPlacement(
            canonicalSlot,
            new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.Unclassified,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 0,
                DomainTag = canonicalSlot.Placement.DomainTag
            });

        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            tamperedSlots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);
        AuxiliaryClusterReservation systemReservation = Assert.Single(clusterPreparation.AuxiliaryReservations);
        Assert.Equal(AuxiliaryClusterKind.System, systemReservation.Kind);
        Assert.Equal((byte)1, systemReservation.SlotMask);

        RuntimeClusterAdmissionPreparation runtimePreparation =
            RuntimeClusterAdmissionPreparation.Create(clusterPreparation);
        RuntimeClusterAdmissionCandidateView candidateView =
            RuntimeClusterAdmissionCandidateView.Create(
                transportFacts.PC,
                tamperedSlots,
                clusterPreparation,
                runtimePreparation);
        RuntimeClusterAdmissionDecisionDraft decisionDraft =
            RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                candidateView,
                clusterPreparedModeEnabled: true);
        RuntimeClusterAdmissionDecisionDraft boundDecisionDraft =
            decisionDraft.BindToCurrentSlot(slotIndex: 0);
        RuntimeClusterAdmissionHandoff handoff = RuntimeClusterAdmissionHandoff.Create(
            transportFacts.PC,
            tamperedSlots,
            clusterPreparation,
            candidateView,
            boundDecisionDraft);

        Assert.Equal((byte)1, candidateView.AuxiliaryReservationMask);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference, decisionDraft.ExecutionMode);
        Assert.True(decisionDraft.UsesIssuePacketAsExecutionSource);
        Assert.True(boundDecisionDraft.UsesIssuePacketAsExecutionSource);
        Assert.Equal((byte)1, handoff.IssuePacket.SelectedSlotMask);
        Assert.Equal((byte)1, handoff.IssuePacket.SelectedNonScalarSlotMask);
        Assert.False(handoff.IssuePacket.HasUnmappedSelectedSlots);
        Assert.False(handoff.IssuePacket.Lane0.IsOccupied);

        IssuePacketLane lane7 = handoff.IssuePacket.Lane7;
        Assert.True(lane7.IsOccupied);
        Assert.Equal((byte)7, lane7.PhysicalLaneIndex);
        Assert.Equal((byte)0, lane7.SlotIndex);
        Assert.Same(microOp, lane7.MicroOp);
        Assert.Equal(SlotClass.SystemSingleton, lane7.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, lane7.PinningKind);
    }

    [Fact]
    public void AdmissionIssueHandoff_UsesAdmissionMemoryFlag_WhenCarrierMemoryFlagIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x2000);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x2000, bundleSerial: 62);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        AtomicMicroOp microOp = Assert.IsType<AtomicMicroOp>(canonicalSlot.MicroOp);

        Assert.True(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.True(canonicalSlot.IsMemoryOp);

        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithFlags(
            canonicalSlot,
            isMemoryOp: false,
            isControlFlow: canonicalSlot.IsControlFlow);

        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            tamperedSlots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);

        AuxiliaryClusterReservation memoryReservation = Assert.Single(clusterPreparation.AuxiliaryReservations);
        Assert.Equal(AuxiliaryClusterKind.Memory, memoryReservation.Kind);
        Assert.Equal((byte)1, memoryReservation.SlotMask);
    }

    [Fact]
    public void AdmissionIssueHandoff_UsesPublishedExecutionHeader_WhenCarrierRuntimeHeaderIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x3000);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.STREAM_WAIT, virtualThreadId: 2));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x3000, bundleSerial: 63);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        StreamControlMicroOp microOp = Assert.IsType<StreamControlMicroOp>(canonicalSlot.MicroOp);

        microOp.VirtualThreadId = 2;
        microOp.OwnerThreadId = 2;

        Assert.Equal(2, microOp.VirtualThreadId);
        Assert.Equal(2, microOp.OwnerThreadId);
        Assert.Equal(canonicalSlot.OpCode, microOp.OpCode);

        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithRuntimeHeader(
            canonicalSlot,
            virtualThreadId: 0,
            ownerThreadId: 1,
            opCode: (uint)InstructionsEnum.ADDI);

        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            tamperedSlots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);
        RuntimeClusterAdmissionPreparation runtimePreparation =
            RuntimeClusterAdmissionPreparation.Create(clusterPreparation);
        RuntimeClusterAdmissionCandidateView candidateView =
            RuntimeClusterAdmissionCandidateView.Create(
                transportFacts.PC,
                tamperedSlots,
                clusterPreparation,
                runtimePreparation);
        RuntimeClusterAdmissionDecisionDraft decisionDraft =
            RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                candidateView,
                clusterPreparedModeEnabled: true);
        RuntimeClusterAdmissionHandoff handoff = RuntimeClusterAdmissionHandoff.Create(
            transportFacts.PC,
            tamperedSlots,
            clusterPreparation,
            candidateView,
            decisionDraft);

        IssuePacketLane lane7 = handoff.IssuePacket.Lane7;
        Assert.True(lane7.IsOccupied);
        Assert.Same(microOp, lane7.MicroOp);
        Assert.Equal(microOp.VirtualThreadId, lane7.VirtualThreadId);
        Assert.Equal(microOp.OwnerThreadId, lane7.OwnerThreadId);
        Assert.Equal(microOp.OpCode, lane7.OpCode);
        Assert.NotEqual(0, lane7.VirtualThreadId);
        Assert.NotEqual(1, lane7.OwnerThreadId);
        Assert.NotEqual((uint)InstructionsEnum.ADDI, lane7.OpCode);
    }

    [Fact]
    public void AdmissionIssueHandoff_WhenFenceOccupiesLane7_ThenExecuteMaskKeepsSystemSingleton()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x3800);

        VLIW_Instruction[] rawSlots = CreateBundle(
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 0, immediate: 10),
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 2, rs1: 0, immediate: 20),
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 0, immediate: 30),
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 4, rs1: 0, immediate: 40),
            new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.FENCE,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                VirtualThreadId = 0
            });

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x3800, bundleSerial: 64);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        RuntimeClusterAdmissionHandoff handoff = BuildAdmissionHandoff(transportFacts);

        IssuePacketLane lane7 = handoff.IssuePacket.Lane7;
        Assert.True(lane7.IsOccupied);
        Assert.Equal((byte)7, lane7.PhysicalLaneIndex);
        Assert.Equal((byte)4, lane7.SlotIndex);
        Assert.IsType<SysEventMicroOp>(lane7.MicroOp);

        var executableMasks = core.TestResolveExecutableIssuePacketMasks(
            handoff.IssuePacket,
            handoff.DependencySummary);

        Assert.Equal((byte)(1 << 7), executableMasks.ExecutableNonScalarPhysicalLaneMask);
        Assert.Equal((byte)(1 << 4), executableMasks.ExecutableNonScalarSlotMask);
        Assert.Equal(
            handoff.IssuePacket.PreparedPhysicalLaneCount,
            handoff.IssuePacket.PreparedScalarLaneCount + CountBits(executableMasks.ExecutableNonScalarPhysicalLaneMask));
    }

    private static RuntimeClusterAdmissionHandoff BuildAdmissionHandoff(
        in DecodedBundleTransportFacts transportFacts,
        byte currentSlotIndex = 0)
    {
        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            transportFacts.Slots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);
        RuntimeClusterAdmissionPreparation runtimePreparation =
            RuntimeClusterAdmissionPreparation.Create(clusterPreparation);
        RuntimeClusterAdmissionCandidateView candidateView =
            RuntimeClusterAdmissionCandidateView.Create(
                transportFacts.PC,
                transportFacts.Slots,
                clusterPreparation,
                runtimePreparation);
        RuntimeClusterAdmissionDecisionDraft decisionDraft =
            RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
                candidateView,
                clusterPreparedModeEnabled: true)
            .BindToCurrentSlot(currentSlotIndex);

        return RuntimeClusterAdmissionHandoff.Create(
            transportFacts.PC,
            transportFacts.Slots,
            clusterPreparation,
            candidateView,
            decisionDraft);
    }

    private static DecodedBundleSlotDescriptor WithPlacement(
        in DecodedBundleSlotDescriptor slot,
        SlotPlacementMetadata placement)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            slot.VirtualThreadId,
            slot.OwnerThreadId,
            slot.OpCode,
            slot.ReadRegisters,
            slot.WriteRegisters,
            slot.WritesRegister,
            slot.IsMemoryOp,
            slot.IsControlFlow,
            placement,
            slot.MemoryBankIntent,
            slot.IsFspInjected,
            slot.IsEmptyOrNop);
    }

    private static DecodedBundleSlotDescriptor WithFlags(
        in DecodedBundleSlotDescriptor slot,
        bool isMemoryOp,
        bool isControlFlow)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            slot.VirtualThreadId,
            slot.OwnerThreadId,
            slot.OpCode,
            slot.ReadRegisters,
            slot.WriteRegisters,
            slot.WritesRegister,
            isMemoryOp,
            isControlFlow,
            slot.Placement,
            slot.MemoryBankIntent,
            slot.IsFspInjected,
            slot.IsEmptyOrNop);
    }

    private static DecodedBundleSlotDescriptor WithRuntimeHeader(
        in DecodedBundleSlotDescriptor slot,
        int virtualThreadId,
        int ownerThreadId,
        uint opCode)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            virtualThreadId,
            ownerThreadId,
            opCode,
            slot.ReadRegisters,
            slot.WriteRegisters,
            slot.WritesRegister,
            slot.IsMemoryOp,
            slot.IsControlFlow,
            slot.Placement,
            slot.MemoryBankIntent,
            slot.IsFspInjected,
            slot.IsEmptyOrNop);
    }

    private static int CountBits(byte mask)
    {
        int count = 0;
        while (mask != 0)
        {
            count += mask & 1;
            mask >>= 1;
        }

        return count;
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] slots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < slots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = slots[slotIndex];
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
}

