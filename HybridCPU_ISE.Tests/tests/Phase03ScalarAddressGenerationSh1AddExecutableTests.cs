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
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class ScalarAddressGenerationSh1AddExecutableTests
{
    [Fact]
    public void Sh1Add_OpcodeValueAndSupportStatus_AreStableAndRuntimeClosed()
    {
        Assert.Equal(53, (int)InstructionsEnum.CZERO_EQZ);
        Assert.Equal(54, (int)InstructionsEnum.CLZ);
        Assert.Equal(56, (int)InstructionsEnum.SH1ADD);
        Assert.Equal((ushort)InstructionsEnum.SH1ADD, IsaOpcodeValues.SH1ADD);
        Assert.Null(OpcodeRegistry.GetInfo(55u));

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "SH1ADD",
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarAddressGeneration", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains("SH1ADD", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("SH1ADD", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain("SH1ADD", IsaV4Surface.OptionalDisabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["SH1ADD"]);
    }

    [Fact]
    public void Sh1Add_ClassifierRegistryAndMaterializer_PublishScalarAluMicroOp()
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.SH1ADD));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.SH1ADD);
        Assert.NotNull(info);
        Assert.Equal("SH1ADD", info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand, info.Value.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.SH1ADD));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.SH1ADD);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SH1ADD));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.SH1ADD,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.SH1ADD,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = rs2,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.SH1ADD, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);
        Assert.False(scalar.IsMemoryOp);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.SH1ADD,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.SH1ADD,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = rs2,
                    HasImmediate = true,
                    Immediate = 1,
                }));
    }

    [Fact]
    public void Sh1Add_DecoderIrAndProjector_UseCanonicalRegisterPayloadOnly()
    {
        const byte rd = 9;
        const byte rs1 = 4;
        const byte rs2 = 10;
        const ulong pc = 0x6600UL;

        VLIW_Instruction[] rawSlots = CreateBundle(
            CreateScalarInstruction(
                InstructionsEnum.SH1ADD,
                rd,
                rs1,
                rs2));

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: pc, bundleSerial: 66);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.SH1ADD, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(rs2, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
        Assert.Null(ir.VectorPayload);
        Assert.Null(ir.DmaStreamComputeDescriptor);
        Assert.Null(ir.AcceleratorCommandDescriptor);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);
        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)InstructionsEnum.SH1ADD, microOp.OpCode);
        Assert.Equal(rd, microOp.DestRegID);
        Assert.Equal(rs1, microOp.Src1RegID);
        Assert.Equal(rs2, microOp.Src2RegID);
        Assert.False(microOp.UsesImmediate);

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            InstructionsEnum.SH1ADD,
            rd,
            rs1,
            rs2,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x6620, 67));
    }

    [Theory]
    [InlineData(0UL, 0UL, 0UL)]
    [InlineData(1UL, 2UL, 4UL)]
    [InlineData(0x4000_0000_0000_0000UL, 5UL, 0x8000_0000_0000_0005UL)]
    [InlineData(0x8000_0000_0000_0000UL, 7UL, 7UL)]
    [InlineData(ulong.MaxValue, 3UL, 1UL)]
    public void Sh1Add_ScalarAluOps_DefinesXlen64WraparoundSemantics(
        ulong shiftedSource,
        ulong addend,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)InstructionsEnum.SH1ADD,
            shiftedSource,
            addend,
            immediate: 0);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(2, 3UL, 0x100UL, 0x106UL)]
    [InlineData(3, 0x8000_0000_0000_0000UL, 0x1234UL, 0x1234UL)]
    public void Sh1Add_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        int vtId,
        ulong shiftedSourceValue,
        ulong addendValue,
        ulong expectedResult)
    {
        const ulong pc = 0x6700UL;
        const ushort shiftedSourceRegister = 5;
        const ushort addendRegister = 6;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, shiftedSourceRegister, shiftedSourceValue);
        core.WriteCommittedArch(vtId, addendRegister, addendValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.SH1ADD,
                rd: (byte)destinationRegister,
                rs1: (byte)shiftedSourceRegister,
                rs2: (byte)addendRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)InstructionsEnum.SH1ADD, decodeStatus.OpCode);
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
    public void Sh1Add_WriteToX0_IsDiscardedAtRetire()
    {
        const int vtId = 1;
        const ulong pc = 0x6800UL;
        const ushort shiftedSourceRegister = 8;
        const ushort addendRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, shiftedSourceRegister, 0x100UL);
        core.WriteCommittedArch(vtId, addendRegister, 0x20UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.SH1ADD,
                rd: 0,
                rs1: (byte)shiftedSourceRegister,
                rs2: (byte)addendRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0x100UL, core.ReadArch(vtId, shiftedSourceRegister));
        Assert.Equal(0x20UL, core.ReadArch(vtId, addendRegister));
    }

    [Fact]
    public void Sh1Add_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation()
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;
        const ulong shiftedSourceValue = 0x40UL;
        const ulong addendValue = 0x1000UL;
        const ulong expectedResult = 0x1080UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, shiftedSourceValue);
        core.WriteCommittedArch(vtId, rs2, addendValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(InstructionsEnum.SH1ADD, rd, rs1, rs2);

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
    public void Sh1Add_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth()
    {
        const int vtId = 2;
        const ulong pc = 0x6900UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, 0x20UL);
        core.WriteCommittedArch(vtId, 6, 0x1000UL);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.SH1ADD,
                rd: (byte)destinationRegister,
                rs1: 5,
                rs2: 6,
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

        Assert.Equal(0x1040UL, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void Sh1Add_AddressGenerationPhase03Pool_IsRuntimeClosedThroughSharedScalarModel()
    {
        string[] promoted =
        [
            "SH2ADD",
            "SH3ADD",
            "ADD.UW",
            "SH1ADD.UW",
            "SH2ADD.UW",
            "SH3ADD.UW",
            "SLLI.UW"
        ];

        foreach (string mnemonic in promoted)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.Equal("ScalarAddressGeneration", status.ExtensionName);
            Assert.True(status.IsExecutableClaim);
            Assert.True(status.HasNumericOpcode);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(HasEnumOrRegistryMnemonic(mnemonic));
        }
    }

    [Fact]
    public void Sh1Add_AddressGenerationCompilerHelpersOpenWhileAdjacentContoursRemainClosed()
    {
        string[] scalarClosed =
        [
            "POPCNT",
            "RDTIME",
            "RDINSTRET",
            "PAUSE",
            "CRC32",
            "CRC64",
            "ADC",
            "SBC",
            "ADDC",
            "SUBC",
            "SFENCE.VMA",
            "DCACHE_CLEAN",
            "ICACHE_INVAL",
        ];

        foreach (string mnemonic in scalarClosed)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.NotEqual(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.False(status.IsExecutableClaim, mnemonic);
        }

        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.Contains("InstructionsEnum.SH1ADD", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.SH2ADD", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.SH3ADD", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.ADD_UW", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.SH1ADD_UW", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.SH2ADD_UW", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.SH3ADD_UW", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.SLLI_UW", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ShiftLeftOneAndAdd", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ShiftLeftTwoAndAdd", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ShiftLeftThreeAndAdd", compilerSource, StringComparison.Ordinal);
        Assert.Contains("AddUnsignedWord", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ShiftLeftOneAndAddUnsignedWord", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ShiftLeftTwoAndAddUnsignedWord", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ShiftLeftThreeAndAddUnsignedWord", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ShiftLeftUnsignedWordByImmediate", compilerSource, StringComparison.Ordinal);

        Assert.DoesNotContain("AddUw", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sh1AddUw", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sh2AddUw", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sh3AddUw", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SlliUw", compilerSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sh1Add_DoesNotReviveLegacyXfmacSlot55()
    {
        var decoder = new VliwDecoderV4();
        VLIW_Instruction instruction = CreateScalarInstruction(
            (InstructionsEnum)55,
            rd: 1,
            rs1: 2,
            rs2: 3);

        InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(() =>
            decoder.Decode(in instruction, slotIndex: 0));

        Assert.Contains("XFMAC", exception.Message, StringComparison.Ordinal);
    }

    private static MicroOpScheduler PrimeReplayScheduler(
        ref Processor.CPU_Core core,
        ulong retiredPc,
        out long serializingEpochCountBefore)
    {
        core.TestInitializeFSPScheduler();
        core.TestPrimeReplayPhase(
            pc: retiredPc,
            totalIterations: 8,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

        MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x6A00, bundleSerial: 68);
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
        byte rs1,
        byte rs2)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
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

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        return hasEnum || hasRegistryMnemonic;
    }

}
