using HybridCPU_ISE.Arch;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03LegacyMoveFamilyPublicationTailTests
{
    [Fact]
    public void DirectFactoryLegacyLoad_ProjectsMemoryLsuTruthBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA400);

        LoadMicroOp loadMicroOp = CreateLegacyLoadMicroOp(
            destinationRegister: 7,
            address: 0x140);

        core.TestSetDecodedBundle(loadMicroOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(loadMicroOp, slot.MicroOp);
        Assert.Equal(InstructionClass.Memory, loadMicroOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, loadMicroOp.SerializationClass);
        Assert.Equal(SlotClass.LsuClass, loadMicroOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, loadMicroOp.AdmissionMetadata.Placement.PinningKind);
        Assert.True(loadMicroOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(loadMicroOp.AdmissionMetadata.HasSideEffects);
        Assert.True(loadMicroOp.AdmissionMetadata.WritesRegister, Describe(loadMicroOp));
        Assert.Empty(loadMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 7 }, loadMicroOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(0x140UL, loadMicroOp.Address);
        Assert.Equal(VLIW_Instruction.NoReg, loadMicroOp.BaseRegID);
        Assert.Equal(SlotClass.LsuClass, slot.Placement.RequiredSlotClass);
        Assert.True(slot.IsMemoryOp);
        Assert.True(slot.WritesRegister);
        Assert.Empty(slot.ReadRegisters);
        Assert.Equal(new[] { 7 }, slot.WriteRegisters);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
    }

    [Fact]
    public void DirectFactoryLegacyStore_ProjectsMemorySideEffectsBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA440);

        StoreMicroOp storeMicroOp = CreateLegacyStoreMicroOp(
            sourceRegister: 5,
            address: 0x180);

        core.TestSetDecodedBundle(storeMicroOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(storeMicroOp, slot.MicroOp);
        Assert.Equal(InstructionClass.Memory, storeMicroOp.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, storeMicroOp.SerializationClass);
        Assert.Equal(SlotClass.LsuClass, storeMicroOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.True(storeMicroOp.AdmissionMetadata.IsMemoryOp);
        Assert.True(storeMicroOp.AdmissionMetadata.HasSideEffects);
        Assert.False(storeMicroOp.AdmissionMetadata.WritesRegister);
        Assert.True(
            storeMicroOp.AdmissionMetadata.ReadRegisters.SequenceEqual(new[] { 5 }),
            Describe(storeMicroOp));
        Assert.Empty(storeMicroOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(0x180UL, storeMicroOp.Address);
        Assert.Equal(VLIW_Instruction.NoReg, storeMicroOp.BaseRegID);
        Assert.Equal(SlotClass.LsuClass, slot.Placement.RequiredSlotClass);
        Assert.True(slot.IsMemoryOp);
        Assert.False(slot.WritesRegister);
        Assert.Equal(new[] { 5 }, slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
    }

    [Fact]
    public void DirectFactoryRetainedRegisterMove_ProjectsScalarReadWriteTruthBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA460);

        MoveMicroOp moveMicroOp = CreateRetainedRegisterMoveMicroOp(
            sourceRegister: 5,
            destinationRegister: 7);

        core.TestSetDecodedBundle(moveMicroOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(moveMicroOp, slot.MicroOp);
        Assert.Equal(InstructionClass.ScalarAlu, moveMicroOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, moveMicroOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, moveMicroOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, moveMicroOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(moveMicroOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(moveMicroOp.AdmissionMetadata.HasSideEffects);
        Assert.True(moveMicroOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(new[] { 5 }, moveMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 7 }, moveMicroOp.AdmissionMetadata.WriteRegisters);
        Assert.False(slot.IsMemoryOp);
        Assert.True(slot.WritesRegister);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(new[] { 5 }, slot.ReadRegisters);
        Assert.Equal(new[] { 7 }, slot.WriteRegisters);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.AuxiliaryOpMask);
    }

    [Fact]
    public void DirectFactoryRetainedImmediateMove_ProjectsScalarWriteOnlyTruthBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA470);

        MoveMicroOp moveMicroOp = CreateRetainedImmediateMoveMicroOp(
            destinationRegister: 9,
            immediate: 0x1234);

        core.TestSetDecodedBundle(moveMicroOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(moveMicroOp, slot.MicroOp);
        Assert.Equal(InstructionClass.ScalarAlu, moveMicroOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, moveMicroOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, moveMicroOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, moveMicroOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(moveMicroOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(moveMicroOp.AdmissionMetadata.HasSideEffects);
        Assert.True(moveMicroOp.AdmissionMetadata.WritesRegister);
        Assert.Empty(moveMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 9 }, moveMicroOp.AdmissionMetadata.WriteRegisters);
        Assert.False(slot.IsMemoryOp);
        Assert.True(slot.WritesRegister);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Empty(slot.ReadRegisters);
        Assert.Equal(new[] { 9 }, slot.WriteRegisters);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.AuxiliaryOpMask);
    }

    [Fact]
    public void DirectFactoryRetainedMove_WithoutProjectedDecoderContextDataType_FailsClosed()
    {
        VLIW_Instruction instruction =
            CreateRetainedRegisterMoveInstruction(sourceRegister: 5, destinationRegister: 7);

        DecoderContext context = new()
        {
            OpCode = (uint)InstructionsEnum.Move,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));

        Assert.Contains("projected DecoderContext data-type handoff", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("Raw VLIW_Instruction.DataType fallback is retired", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void DirectFactoryLegacyLoad_NoDestinationNoArchReg_FailsClosedBeforeManualPublication()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Load,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Src2Pointer = 0x140,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        DecoderContext context = new()
        {
            OpCode = (uint)InstructionsEnum.Load,
            Reg1ID = VLIW_Instruction.NoArchReg,
            Reg2ID = VLIW_Instruction.NoReg,
            Reg3ID = VLIW_Instruction.NoReg,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Load, context));

        Assert.Contains("legacy Load", exception.Message);
        Assert.Contains("destination", exception.Message);
        Assert.Contains("fail closed", exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DirectFactoryLegacyStore_MissingSourceRegister_FailsClosedBeforeManualPublication(
        bool useDirectManualSourceField)
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Store,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Src2Pointer = 0x180,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        DecoderContext context = new()
        {
            OpCode = (uint)InstructionsEnum.Store,
            Reg1ID = useDirectManualSourceField
                ? VLIW_Instruction.NoArchReg
                : VLIW_Instruction.NoReg,
            Reg2ID = VLIW_Instruction.NoReg,
            Reg3ID = useDirectManualSourceField
                ? (ushort)5
                : VLIW_Instruction.NoArchReg,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Store, context));

        Assert.Contains("legacy Store", exception.Message);
        Assert.Contains("source", exception.Message);
        Assert.Contains("fail closed", exception.Message);
    }

    [Fact]
    public void DirectFactoryLegacyLoad_RoutesAsMemoryAuxiliaryInIssueHandoff()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA480);

        var scalarMicroOp = new ScalarALUMicroOp
        {
            OpCode = (uint)InstructionsEnum.ADDI,
            DestRegID = 1,
            Src1RegID = 2,
            Immediate = 4,
            UsesImmediate = true,
            WritesRegister = true
        };
        scalarMicroOp.InitializeMetadata();

        LoadMicroOp loadMicroOp = CreateLegacyLoadMicroOp(
            destinationRegister: 9,
            address: 0x1C0);

        core.TestSetDecodedBundle(scalarMicroOp, loadMicroOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            transportFacts.Slots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);

        AuxiliaryClusterReservation memoryReservation = Assert.Single(clusterPreparation.AuxiliaryReservations);
        Assert.Equal(AuxiliaryClusterKind.Memory, memoryReservation.Kind);
        Assert.Equal((byte)0b0000_0010, memoryReservation.SlotMask);

        RuntimeClusterAdmissionHandoff handoff = BuildAdmissionHandoff(transportFacts, currentSlotIndex: 0);

        Assert.Equal((byte)0b0000_0011, handoff.IssuePacket.SelectedSlotMask);
        Assert.Equal((byte)0b0000_0010, handoff.IssuePacket.SelectedNonScalarSlotMask);
        Assert.False(handoff.IssuePacket.HasUnmappedSelectedSlots);
        Assert.True(handoff.IssuePacket.Lane0.IsOccupied);
        Assert.True(handoff.IssuePacket.Lane4.IsOccupied);
        Assert.Same(loadMicroOp, handoff.IssuePacket.Lane4.MicroOp);
        Assert.Equal(SlotClass.LsuClass, handoff.IssuePacket.Lane4.RequiredSlotClass);
    }

    [Fact]
    public void DirectFactoryRetainedTripleDestinationMoveDt5_FailsClosedBeforeManualPublication()
    {
        VLIW_Instruction instruction = CreateRetainedTripleDestinationMoveInstruction(
            destinationRegister1: 8,
            destinationRegister2: 9,
            destinationRegister3: 10,
            operand1: 0x180,
            operand2: 0x1C0);
        DecoderContext context = CreateDecoderContext(in instruction);

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));

        Assert.Contains("Move DT=5", exception.Message);
    }

    [Fact]
    public void DirectFactoryRetainedDualWriteMoveDt4_FailsClosedBeforeManualPublication()
    {
        VLIW_Instruction instruction = CreateRetainedDualWriteMoveInstruction(
            destinationRegister1: 8,
            destinationRegister2: 9,
            operand1: 0x180,
            operand2: 0x1C0);
        DecoderContext context = CreateDecoderContext(in instruction);

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));

        Assert.Contains("Move DT=4", exception.Message);
    }

    [Fact]
    public void DirectFactoryRetainedLegacyStoreMoveDt2_FailsClosedBeforeManualPublication()
    {
        VLIW_Instruction instruction = CreateRetainedLegacyStoreMoveInstruction(
            sourceRegister: 8,
            address: 0x180);
        DecoderContext context = CreateDecoderContext(in instruction);

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));

        Assert.Contains("Move DT=2", exception.Message);
        Assert.Contains("Load/Store", exception.Message);
    }

    [Fact]
    public void DirectFactoryRetainedLegacyLoadMoveDt3_FailsClosedBeforeManualPublication()
    {
        VLIW_Instruction instruction = CreateRetainedLegacyLoadMoveInstruction(
            destinationRegister: 8,
            address: 0x1C0);
        DecoderContext context = CreateDecoderContext(in instruction);

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));

        Assert.Contains("Move DT=3", exception.Message);
        Assert.Contains("Load/Store", exception.Message);
    }

    [Fact]
    public void DirectFactoryRetainedMoveNumOpcode_ProjectsScalarWriteOnlyTruthBeforeManualPublication()
    {
        VLIW_Instruction instruction = CreateRetainedMoveNumInstruction(
            destinationRegister: 9,
            immediate: 0x5678);
        DecoderContext context = CreateDecoderContext(in instruction);

        MoveMicroOp moveMicroOp = Assert.IsType<MoveMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move_Num, context));

        Assert.Equal(InstructionClass.ScalarAlu, moveMicroOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, moveMicroOp.SerializationClass);
        Assert.False(moveMicroOp.AdmissionMetadata.IsMemoryOp);
        Assert.True(moveMicroOp.AdmissionMetadata.WritesRegister);
        Assert.Empty(moveMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 9 }, moveMicroOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void DirectFactoryRetainedRegisterMove_PrefersDecoderContextMoveShapeOverRawInstructionPayload()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA490);
        core.RetireCoordinator.Retire(
            YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord.RegisterWrite(0, 5, 0xABCD));

        VLIW_Instruction instruction = CreateRetainedImmediateMoveInstruction(
            destinationRegister: 9,
            immediate: 0x1234);
        DecoderContext context = CreateDecoderContext(in instruction);
        context.DataType = 0;
        context.HasDataType = true;
        context.Reg1ID = 5;
        context.Reg2ID = 7;
        context.AuxData = 0x5555;

        MoveMicroOp moveMicroOp = Assert.IsType<MoveMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));

        Assert.Equal(new[] { 5 }, moveMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 7 }, moveMicroOp.AdmissionMetadata.WriteRegisters);

        moveMicroOp.OwnerThreadId = 0;
        Assert.True(moveMicroOp.Execute(ref core));
        Assert.Equal((ushort)7, moveMicroOp.DestRegID);
        Assert.True(moveMicroOp.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(0xABCDUL, value);
    }

    [Fact]
    public void DirectFactoryRetainedImmediateMove_PrefersDecoderContextMoveShapeOverRawInstructionPayload()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA4A0);
        core.RetireCoordinator.Retire(
            YAKSys_Hybrid_CPU.Core.Registers.Retire.RetireRecord.RegisterWrite(0, 5, 0xABCD));

        VLIW_Instruction instruction = CreateRetainedRegisterMoveInstruction(
            sourceRegister: 5,
            destinationRegister: 7);
        DecoderContext context = CreateDecoderContext(in instruction);
        context.DataType = 1;
        context.HasDataType = true;
        context.Reg1ID = 9;
        context.Reg2ID = VLIW_Instruction.NoReg;
        context.AuxData = 0x1234;

        MoveMicroOp moveMicroOp = Assert.IsType<MoveMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));

        Assert.Empty(moveMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 9 }, moveMicroOp.AdmissionMetadata.WriteRegisters);

        moveMicroOp.OwnerThreadId = 0;
        Assert.True(moveMicroOp.Execute(ref core));
        Assert.Equal((ushort)9, moveMicroOp.DestRegID);
        Assert.True(moveMicroOp.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(0x1234UL, value);
    }

    [Fact]
    public void DirectFactoryRetainedMoveNumOpcode_ProjectsImmediateMoveShapeWithoutRequiringRawInstructionPayload()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA4B0);

        VLIW_Instruction instruction = CreateRetainedMoveNumInstruction(
            destinationRegister: 9,
            immediate: 0x5678);
        DecoderContext context = CreateDecoderContext(in instruction);

        MoveMicroOp moveMicroOp = Assert.IsType<MoveMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move_Num, context));

        Assert.Empty(moveMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 9 }, moveMicroOp.AdmissionMetadata.WriteRegisters);

        moveMicroOp.OwnerThreadId = 0;
        Assert.True(moveMicroOp.Execute(ref core));
        Assert.Equal((ushort)9, moveMicroOp.DestRegID);
        Assert.True(moveMicroOp.TryGetPrimaryWriteBackResult(out ulong value));
        Assert.Equal(0x5678UL, value);
    }

    private static RuntimeClusterAdmissionHandoff BuildAdmissionHandoff(
        in DecodedBundleTransportFacts transportFacts,
        byte currentSlotIndex)
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

    private static LoadMicroOp CreateLegacyLoadMicroOp(
        byte destinationRegister,
        ulong address)
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Load,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.Load,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<LoadMicroOp>(InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Load, context));
    }

    private static MoveMicroOp CreateRetainedRegisterMoveMicroOp(
        byte sourceRegister,
        byte destinationRegister)
    {
        VLIW_Instruction instruction =
            CreateRetainedRegisterMoveInstruction(sourceRegister, destinationRegister);
        DecoderContext context = CreateDecoderContext(in instruction);
        return Assert.IsType<MoveMicroOp>(InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));
    }

    private static MoveMicroOp CreateRetainedImmediateMoveMicroOp(
        byte destinationRegister,
        ulong immediate)
    {
        VLIW_Instruction instruction =
            CreateRetainedImmediateMoveInstruction(destinationRegister, immediate);
        DecoderContext context = CreateDecoderContext(in instruction);
        return Assert.IsType<MoveMicroOp>(InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Move, context));
    }

    private static StoreMicroOp CreateLegacyStoreMicroOp(
        byte sourceRegister,
        ulong address)
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Store,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                sourceRegister),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.Store,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<StoreMicroOp>(InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Store, context));
    }

    private static VLIW_Instruction CreateRetainedRegisterMoveInstruction(
        byte sourceRegister,
        byte destinationRegister)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 0,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                sourceRegister,
                destinationRegister,
                VLIW_Instruction.NoArchReg),
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedImmediateMoveInstruction(
        byte destinationRegister,
        ulong immediate)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 1,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedMoveNumInstruction(
        byte destinationRegister,
        ulong immediate)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move_Num,
            DataType = 0,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedLegacyStoreMoveInstruction(
        byte sourceRegister,
        ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 2,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                sourceRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedLegacyLoadMoveInstruction(
        byte destinationRegister,
        ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 3,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedDualWriteMoveInstruction(
        byte destinationRegister1,
        byte destinationRegister2,
        ulong operand1,
        ulong operand2)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 4,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister1,
                destinationRegister2,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = operand1,
            Word3 = operand2,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedTripleDestinationMoveInstruction(
        byte destinationRegister1,
        byte destinationRegister2,
        byte destinationRegister3,
        ulong operand1,
        ulong operand2)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 5,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister1,
                destinationRegister2,
                destinationRegister3),
            Src2Pointer = operand1,
            Word3 = operand2,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static DecoderContext CreateDecoderContext(
        in VLIW_Instruction instruction)
    {
        return new DecoderContext
        {
            OpCode = instruction.OpCode,
            DataType = instruction.DataType,
            HasDataType = true,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };
    }

    private static string Describe(LoadMicroOp microOp)
    {
        return $"Opcode={microOp.OpCode}, Address=0x{microOp.Address:X}, Dest={microOp.DestRegID}, " +
               $"Writes={microOp.WritesRegister}, Memory={microOp.IsMemoryOp}, SideEffects={microOp.HasSideEffects}, " +
               $"Read=[{string.Join(",", microOp.ReadRegisters)}], Write=[{string.Join(",", microOp.WriteRegisters)}], " +
               $"AdmWrites={microOp.AdmissionMetadata.WritesRegister}, AdmRead=[{string.Join(",", microOp.AdmissionMetadata.ReadRegisters)}], " +
               $"AdmWrite=[{string.Join(",", microOp.AdmissionMetadata.WriteRegisters)}]";
    }

    private static string Describe(StoreMicroOp microOp)
    {
        return $"Opcode={microOp.OpCode}, Address=0x{microOp.Address:X}, Src={microOp.SrcRegID}, " +
               $"Writes={microOp.WritesRegister}, Memory={microOp.IsMemoryOp}, SideEffects={microOp.HasSideEffects}, " +
               $"Read=[{string.Join(",", microOp.ReadRegisters)}], Write=[{string.Join(",", microOp.WriteRegisters)}], " +
               $"AdmWrites={microOp.AdmissionMetadata.WritesRegister}, AdmRead=[{string.Join(",", microOp.AdmissionMetadata.ReadRegisters)}], " +
               $"AdmWrite=[{string.Join(",", microOp.AdmissionMetadata.WriteRegisters)}]";
    }
}

