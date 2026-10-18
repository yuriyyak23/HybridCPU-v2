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

public sealed class ScalarOptionalCzeroEqzExecutableTests
{
    [Fact]
    public void CzeroEqz_OpcodeValueAndSupportStatus_AreStableAndRuntimeClosed()
    {
        Assert.Equal(53, (int)InstructionsEnum.CZERO_EQZ);
        Assert.Equal((ushort)InstructionsEnum.CZERO_EQZ, IsaOpcodeValues.CZERO_EQZ);

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "CZERO.EQZ",
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarSelectCzero", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains("CZERO.EQZ", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("CZERO.EQZ", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain("CZERO.EQZ", IsaV4Surface.OptionalDisabledOpcodes);
    }

    [Fact]
    public void CzeroEqz_ClassifierRegistryAndMaterializer_PublishScalarAluMicroOp()
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.CZERO_EQZ));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.CZERO_EQZ);
        Assert.NotNull(info);
        Assert.Equal("CZERO.EQZ", info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.CZERO_EQZ));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.CZERO_EQZ);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.CZERO_EQZ));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.CZERO_EQZ,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.CZERO_EQZ,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = rs2,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.CZERO_EQZ, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);
        Assert.False(scalar.IsMemoryOp);
    }

    [Fact]
    public void CzeroEqz_DecoderIrAndProjector_UseCanonicalRegisterPayloadOnly()
    {
        const byte rd = 9;
        const byte rs1 = 4;
        const byte rs2 = 10;
        const ulong pc = 0x5300UL;

        VLIW_Instruction[] rawSlots = CreateBundle(
            CreateScalarInstruction(
                InstructionsEnum.CZERO_EQZ,
                rd,
                rs1,
                rs2));

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: pc, bundleSerial: 53);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.CZERO_EQZ, ir.CanonicalOpcode.Value);
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
        Assert.Equal((uint)InstructionsEnum.CZERO_EQZ, microOp.OpCode);
        Assert.Equal(rd, microOp.DestRegID);
        Assert.Equal(rs1, microOp.Src1RegID);
        Assert.Equal(rs2, microOp.Src2RegID);
        Assert.False(microOp.UsesImmediate);
    }

    [Theory]
    [InlineData(0UL, 0UL, 0UL)]
    [InlineData(1UL, 0UL, 0UL)]
    [InlineData(0x0123_4567_89AB_CDEFUL, 0UL, 0UL)]
    [InlineData(0x0123_4567_89AB_CDEFUL, 1UL, 0x0123_4567_89AB_CDEFUL)]
    [InlineData(ulong.MaxValue, 0UL, 0UL)]
    [InlineData(ulong.MaxValue, 0x8000_0000_0000_0000UL, ulong.MaxValue)]
    public void CzeroEqz_ScalarAluOps_DefinesXlen64ZeroSelectEdges(
        ulong source,
        ulong condition,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)InstructionsEnum.CZERO_EQZ,
            source,
            condition,
            immediate: 0);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(2, 0UL, 0UL)]
    [InlineData(3, 0xCAFE_BABE_F00D_1234UL, 0x1234_5678_9ABC_DEF0UL)]
    public void CzeroEqz_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        int vtId,
        ulong condition,
        ulong expectedResult)
    {
        const ulong pc = 0x5400UL;
        const ushort sourceRegister = 5;
        const ushort conditionRegister = 6;
        const ushort destinationRegister = 7;
        const ulong sourceValue = 0x1234_5678_9ABC_DEF0UL;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, conditionRegister, condition);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CZERO_EQZ,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                rs2: (byte)conditionRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)InstructionsEnum.CZERO_EQZ, decodeStatus.OpCode);
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
    public void CzeroEqz_WriteToX0_IsDiscardedAtRetire()
    {
        const int vtId = 1;
        const ulong pc = 0x5500UL;
        const ushort sourceRegister = 8;
        const ushort conditionRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0xFFFF_FFFF_FFFF_FFFFUL);
        core.WriteCommittedArch(vtId, conditionRegister, 1UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CZERO_EQZ,
                rd: 0,
                rs1: (byte)sourceRegister,
                rs2: (byte)conditionRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0xFFFF_FFFF_FFFF_FFFFUL, core.ReadArch(vtId, sourceRegister));
    }

    [Fact]
    public void CzeroEqz_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation()
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;
        const ulong sourceValue = 0xFEED_FACE_CAFE_BEEFUL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);
        core.WriteCommittedArch(vtId, rs2, 1UL);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(InstructionsEnum.CZERO_EQZ, rd, rs1, rs2);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 91,
                vtId);

        Assert.Equal(0UL, core.ReadArch(vtId, rd));
        RetireRecord record = Assert.Single(snapshot.RetireRecords);
        Assert.True(record.IsRegisterWrite);
        Assert.Equal(vtId, record.VtId);
        Assert.Equal(rd, record.ArchReg);
        Assert.Equal(sourceValue, record.Value);
        Assert.False(snapshot.HasTypedEffect);

        RetireWindowCaptureTestHelper.ApplyExecutionDispatcherRetireWindowPublications(
            ref core,
            dispatcher,
            instruction,
            state,
            bundleSerial: 91,
            vtId);

        Assert.Equal(sourceValue, core.ReadArch(vtId, rd));
    }

    [Fact]
    public void CzeroEqz_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth()
    {
        const int vtId = 2;
        const ulong pc = 0x5600UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
        const ulong sourceValue = 0xDEAD_BEEF_DEAD_BEEFUL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, sourceValue);
        core.WriteCommittedArch(vtId, 6, 1UL);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.CZERO_EQZ,
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

        Assert.Equal(sourceValue, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void CzeroEqz_AdjacentScalarOptionalContours_RemainFailClosed()
    {
        string[] reservedOrAbsent =
        [
            "SEQZ",
            "SNEZ",
            "CSEL",
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

        foreach (string mnemonic in reservedOrAbsent)
        {
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic), mnemonic);
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.False(status.IsExecutableClaim, mnemonic);
            Assert.NotEqual(IsaInstructionStatus.OptionalEnabled, status.Status);
        }

        string[] laterBitmanipContours = ["POPCNT"];
        foreach (string mnemonic in laterBitmanipContours)
        {
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic), mnemonic);
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.NotEqual(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.False(status.IsExecutableClaim, mnemonic);
        }
    }

    [Fact]
    public void CzeroEqz_CompilerEmission_OpensConditionalZeroRowsWithoutFacadeAliases()
    {
        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();

        Assert.Contains("InstructionsEnum.CZERO_EQZ", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.CZERO_NEZ", compilerSource, StringComparison.Ordinal);
        Assert.Contains("CZERO.EQZ", compilerSource, StringComparison.Ordinal);
        Assert.Contains("CZERO.NEZ", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ZeroIfConditionEqualZero", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ZeroIfConditionNotEqualZero", compilerSource, StringComparison.Ordinal);

        Assert.DoesNotContain("CzeroEqz", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CzeroNez", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Csel", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConditionalSelect", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Seqz", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Snez", compilerSource, StringComparison.OrdinalIgnoreCase);
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5700, bundleSerial: 57);
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
            DataTypeValue = DataTypeEnum.INT32,
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
