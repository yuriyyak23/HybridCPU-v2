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

namespace HybridCPU_ISE.Tests.Phase03;

public sealed class ScalarCarryLessClmulExecutableTests
{
    [Fact]
    public void Clmul_OpcodeValueAndSupportStatus_AreStableAndRuntimeClosed()
    {
        Assert.Equal(53, (int)InstructionsEnum.CZERO_EQZ);
        Assert.Equal(54, (int)InstructionsEnum.CLZ);
        Assert.Null(OpcodeRegistry.GetInfo(55u));
        Assert.Equal(56, (int)InstructionsEnum.SH1ADD);
        Assert.Equal(57, (int)InstructionsEnum.RDCYCLE);
        Assert.Equal(58, (int)InstructionsEnum.CLMUL);
        Assert.Equal((ushort)InstructionsEnum.CLMUL, IsaOpcodeValues.CLMUL);

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "CLMUL",
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("ScalarCarryLessChecksum", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains("CLMUL", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("CLMUL", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain("CLMUL", IsaV4Surface.OptionalDisabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["CLMUL"]);
    }

    [Fact]
    public void Clmul_ClassifierRegistryAndMaterializer_PublishScalarAluMicroOp()
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.CLMUL));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.CLMUL);
        Assert.NotNull(info);
        Assert.Equal("CLMUL", info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand, info.Value.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.CLMUL));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.CLMUL);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.CLMUL));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.CLMUL,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.CLMUL,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = rs2,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.CLMUL, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);
        Assert.False(scalar.IsMemoryOp);

        Assert.Equal(InternalOpKind.ClMul, new InternalOpBuilder()
            .Build(CreateInstructionIr(InstructionsEnum.CLMUL, rd, rs1, rs2))
            .Kind);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.CLMUL,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.CLMUL,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = rs2,
                    HasImmediate = true,
                    Immediate = 1,
                }));
    }

    [Fact]
    public void Clmul_DecoderIrAndProjector_UseCanonicalRegisterPayloadOnly()
    {
        const byte rd = 9;
        const byte rs1 = 4;
        const byte rs2 = 10;
        const ulong pc = 0x7200UL;

        VLIW_Instruction[] rawSlots = CreateBundle(
            CreateScalarInstruction(
                InstructionsEnum.CLMUL,
                rd,
                rs1,
                rs2));

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: pc, bundleSerial: 82);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.CLMUL, ir.CanonicalOpcode.Value);
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
        Assert.Equal((uint)InstructionsEnum.CLMUL, microOp.OpCode);
        Assert.Equal(rd, microOp.DestRegID);
        Assert.Equal(rs1, microOp.Src1RegID);
        Assert.Equal(rs2, microOp.Src2RegID);
        Assert.False(microOp.UsesImmediate);

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            InstructionsEnum.CLMUL,
            rd,
            rs1,
            rs2,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x7220, 83));
    }

    [Theory]
    [InlineData(0UL, 0UL, 0UL)]
    [InlineData(1UL, 0x1234_5678_9ABC_DEF0UL, 0x1234_5678_9ABC_DEF0UL)]
    [InlineData(2UL, 3UL, 6UL)]
    [InlineData(ulong.MaxValue, ulong.MaxValue, 0x5555_5555_5555_5555UL)]
    [InlineData(0x8000_0000_0000_0000UL, 2UL, 0UL)]
    [InlineData(0xF0F0UL, 0x0F0FUL, 0x0000_0000_0550_0550UL)]
    [InlineData(0x1234_5678_9ABC_DEF0UL, 0x0FED_CBA9_8765_4321UL, 0x40A0_7898_28C8_10F0UL)]
    public void Clmul_ScalarAluOps_DefinesXlen64CarryLessLowHalfSemantics(
        ulong multiplicand,
        ulong multiplier,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)InstructionsEnum.CLMUL,
            multiplicand,
            multiplier,
            immediate: 0);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(2, 2UL, 3UL, 6UL)]
    [InlineData(3, ulong.MaxValue, ulong.MaxValue, 0x5555_5555_5555_5555UL)]
    public void Clmul_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        int vtId,
        ulong multiplicandValue,
        ulong multiplierValue,
        ulong expectedResult)
    {
        const ulong pc = 0x7300UL;
        const ushort multiplicandRegister = 5;
        const ushort multiplierRegister = 6;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, multiplicandRegister, multiplicandValue);
        core.WriteCommittedArch(vtId, multiplierRegister, multiplierValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CLMUL,
                rd: (byte)destinationRegister,
                rs1: (byte)multiplicandRegister,
                rs2: (byte)multiplierRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)InstructionsEnum.CLMUL, decodeStatus.OpCode);
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
    public void Clmul_WriteToX0_IsDiscardedAtRetire()
    {
        const int vtId = 1;
        const ulong pc = 0x7400UL;
        const ushort multiplicandRegister = 8;
        const ushort multiplierRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, multiplicandRegister, 2UL);
        core.WriteCommittedArch(vtId, multiplierRegister, 3UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CLMUL,
                rd: 0,
                rs1: (byte)multiplicandRegister,
                rs2: (byte)multiplierRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(2UL, core.ReadArch(vtId, multiplicandRegister));
        Assert.Equal(3UL, core.ReadArch(vtId, multiplierRegister));
    }

    [Fact]
    public void Clmul_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation()
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;
        const ulong multiplicandValue = 0xF0F0UL;
        const ulong multiplierValue = 0x0F0FUL;
        const ulong expectedResult = 0x0000_0000_0550_0550UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, multiplicandValue);
        core.WriteCommittedArch(vtId, rs2, multiplierValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(InstructionsEnum.CLMUL, rd, rs1, rs2);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 98,
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
            bundleSerial: 98,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Fact]
    public void Clmul_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth()
    {
        const int vtId = 2;
        const ulong pc = 0x7500UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, 2UL);
        core.WriteCommittedArch(vtId, 6, 3UL);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.CLMUL,
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

        Assert.Equal(6UL, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void Clmul_UnselectedCarrylessChecksumAndCarryContours_RemainFailClosed()
    {
        string[] closed =
        [
            "CRC32",
            "CRC64",
            "ADC",
            "SBC",
            "ADDC",
            "SUBC"
        ];

        foreach (string mnemonic in closed)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.IsExecutableClaim);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic), mnemonic);
        }
    }

    [Fact]
    public void Clmul_CompilerEmission_OpensCarryLessRowsWhileAdjacentContoursRemainClosed()
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

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        string compilerEmissionSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.Contains("InstructionsEnum.CLMUL", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.CLMULH", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.CLMULR", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("BinaryPolynomialProductLow", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("BinaryPolynomialProductHigh", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("BinaryPolynomialProductReverse", compilerEmissionSource, StringComparison.Ordinal);
        Assert.Contains("ScalarCarryLessChecksum", compilerEmissionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileClMul", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitClMul", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CarryLessMultiply", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompilerDeferredScalarAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.CRC32", compilerEmissionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.CRC64", compilerEmissionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileCrc32", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileCrc64", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitCrc32", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitCrc64", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileAdc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileSbc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileAddc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileSubc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitAdc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitSbc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitAddc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitSubc", compilerEmissionSource, StringComparison.OrdinalIgnoreCase);
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7600, bundleSerial: 84);
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
