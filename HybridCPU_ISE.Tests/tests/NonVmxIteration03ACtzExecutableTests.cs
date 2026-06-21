using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using CloseToRtlCtz = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount.CtzInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class NonVmxIteration03ACtzExecutableTests
{
    [Fact]
    public void Ctz_OpcodeStatusAndCloseToRtlObject_AreRuntimeClosed()
    {
        Assert.Equal(59, (int)InstructionsEnum.CTZ);
        Assert.Equal((ushort)InstructionsEnum.CTZ, IsaOpcodeValues.CTZ);

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "CTZ",
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarBitmanipCore", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains("CTZ", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("CTZ", IsaV4Surface.OptionalExtensions);
        Assert.DoesNotContain("CTZ", IsaV4Surface.OptionalDisabledOpcodes);

        Assert.Equal("CTZ", CloseToRtlCtz.Mnemonic);
        Assert.Equal(64, CloseToRtlCtz.XLen);
        Assert.Equal((ushort)InstructionsEnum.CTZ, CloseToRtlCtz.Opcode);
        Assert.True(CloseToRtlCtz.WritesScalarRegister);
        Assert.False(CloseToRtlCtz.HasSideEffects);
    }

    [Fact]
    public void Ctz_ClassifierRegistryAndMaterializer_PublishUnaryScalarAluMicroOp()
    {
        const byte rd = 7;
        const byte rs1 = 5;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.CTZ));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.CTZ);
        Assert.NotNull(info);
        Assert.Equal("CTZ", info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
        Assert.Equal(1, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.None, info.Value.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.CTZ));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.CTZ);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.CTZ));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.CTZ,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.CTZ,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = 0,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.CTZ, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { (int)rs1 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);
    }

    [Fact]
    public void Ctz_DecoderIrAndProjector_RequireCanonicalUnaryRegisterPayload()
    {
        const byte rd = 8;
        const byte rs1 = 9;

        VLIW_Instruction instruction = CreateScalarInstruction(
            InstructionsEnum.CTZ,
            rd: rd,
            rs1: rs1);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x6600, bundleSerial: 66);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.CTZ, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.Null(ir.VectorPayload);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)InstructionsEnum.CTZ, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);

        VLIW_Instruction registerAlias = CreateScalarInstruction(
            InstructionsEnum.CTZ,
            rd: rd,
            rs1: rs1,
            rs2: 3);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(registerAlias), 0x6620, 67));

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            InstructionsEnum.CTZ,
            rd: rd,
            rs1: rs1,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x6640, 68));
    }

    [Theory]
    [InlineData(0UL, 64UL)]
    [InlineData(1UL, 0UL)]
    [InlineData(2UL, 1UL)]
    [InlineData(0x8000_0000_0000_0000UL, 63UL)]
    [InlineData(0x0000_0001_0000_0000UL, 32UL)]
    [InlineData(0xFFFF_FFFF_FFFF_FFFFUL, 0UL)]
    public void Ctz_ScalarAluAndCloseToRtlObject_DefineXlen64TrailingZeroEdges(
        ulong source,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)InstructionsEnum.CTZ,
            source,
            op2: 0,
            immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, CloseToRtlCtz.Execute(source));
    }

    [Theory]
    [InlineData(2, 0UL, 64UL)]
    [InlineData(3, 0x0000_0000_0000_0010UL, 4UL)]
    public void Ctz_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        int vtId,
        ulong sourceValue,
        ulong expectedResult)
    {
        const ulong pc = 0x6700UL;
        const ushort sourceRegister = 5;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CTZ,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)InstructionsEnum.CTZ, decodeStatus.OpCode);
        Assert.False(decodeStatus.IsVectorOp);
        Assert.False(decodeStatus.IsMemoryOp);

        core.TestRunExecuteStageFromCurrentDecodeState();

        var executeStatus = core.TestReadExecuteStageStatus();
        Assert.True(executeStatus.Valid);
        Assert.True(executeStatus.ResultReady);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(pc, core.ReadCommittedPc(vtId));
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));

        AssertReplayPhasePreserved(core, scheduler, serializingEpochCountBefore);

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
    }

    [Fact]
    public void Ctz_WriteToX0_IsDiscardedAtRetire()
    {
        const int vtId = 1;
        const ulong pc = 0x6800UL;
        const ushort sourceRegister = 8;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0x0000_0000_0000_0100UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CTZ,
                rd: 0,
                rs1: (byte)sourceRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0x0000_0000_0000_0100UL, core.ReadArch(vtId, sourceRegister));
    }

    [Fact]
    public void Ctz_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation()
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const ulong sourceValue = 0x0000_0000_0000_0100UL;
        const ulong expectedResult = 8UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(InstructionsEnum.CTZ, rd, rs1);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 96,
                vtId);

        Assert.Equal(0UL, core.ReadArch(vtId, rd));
        RetireRecord record = Assert.Single(snapshot.RetireRecords);
        Assert.True(record.IsRegisterWrite);
        Assert.Equal(vtId, record.VtId);
        Assert.Equal(rd, record.ArchReg);
        Assert.Equal(expectedResult, record.Value);
        Assert.False(snapshot.HasTypedEffect);

        RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
            ref core,
            dispatcher,
            instruction,
            state,
            bundleSerial: 96,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Fact]
    public void Ctz_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth()
    {
        const int vtId = 2;
        const ulong pc = 0x6900UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, 0x0000_0000_0000_0010UL);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.CTZ,
                rd: (byte)destinationRegister,
                rs1: 5,
                virtualThreadId: (byte)vtId);
        ScalarALUMicroOp microOp = DecodeAndMaterializeScalar(instruction, vtId);
        HybridCPU_ISE.Core.ReplayToken rollbackToken = microOp.CreateRollbackToken(vtId);
        rollbackToken.CaptureRegisterState(ref core, [(int)destinationRegister]);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: pc,
            admissionExecutionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared);
        core.TestRunMemoryStageFromCurrentExecuteState();
        core.TestLatchMemoryToWriteBackTransferState();
        core.TestRunWriteBackStage();

        Assert.Equal(4UL, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void Ctz_AdjacentPopcntAlias_RemainsFailClosed()
    {
        string[] closed =
        [
            "POPCNT"
        ];

        foreach (string mnemonic in closed)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.NotEqual(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.False(status.IsExecutableClaim);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic));
        }

        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.DoesNotContain("POPCNT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CountPopulation", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PopulationCount", compilerSource, StringComparison.Ordinal);
    }

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumName = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>()
            .Any(name => string.Equals(name, enumName, StringComparison.OrdinalIgnoreCase));
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        return hasEnum || hasRegistryMnemonic;
    }

    private static MicroOpScheduler PrimeReplayScheduler(
        ref Processor.CPU_Core core,
        ulong pc,
        out long serializingEpochCountBefore)
    {
        core.TestInitializeFSPScheduler();
        core.TestPrimeReplayPhase(
            pc: pc,
            totalIterations: 8,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));
        MicroOpScheduler scheduler = core.TestGetFSPScheduler()
            ?? throw new InvalidOperationException("Expected initialized scheduler.");
        var capacityState = new SlotClassCapacityState();
        capacityState.InitializeFromLaneMap();
        scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
        scheduler.TestSetClassTemplateValid(true);
        scheduler.TestSetClassTemplateDomainId(0);
        serializingEpochCountBefore = scheduler.SerializingEpochCount;

        Assert.True(core.GetReplayPhaseContext().IsActive);
        Assert.True(scheduler.TestGetReplayPhaseContext().IsActive);
        return scheduler;
    }

    private static void AssertReplayPhasePreserved(
        Processor.CPU_Core core,
        MicroOpScheduler scheduler,
        long serializingEpochCountBefore)
    {
        ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();
        Assert.True(replayPhase.IsActive);
        Assert.True(schedulerPhase.IsActive);
        Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
    }

    private static ScalarALUMicroOp DecodeAndMaterializeScalar(
        VLIW_Instruction instruction,
        int vtId)
    {
        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x6A00, bundleSerial: 69);
        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
    }

    private static InstructionIR CreateInstructionIr(
        InstructionsEnum opcode,
        byte rd,
        byte rs1)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = rd,
            Rs1 = rs1,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0
        };
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
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = virtualThreadId
        };
    }
}
