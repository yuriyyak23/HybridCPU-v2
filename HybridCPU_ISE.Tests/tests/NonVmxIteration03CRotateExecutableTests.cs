using System;
using System.Collections.Generic;
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
using CloseToRtlRol = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.Rotates.RolInstruction;
using CloseToRtlRor = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.Rotates.RorInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class NonVmxIteration03CRotateExecutableTests
{
    public static IEnumerable<object[]> RotateOpcodeCases()
    {
        yield return new object[] { InstructionsEnum.ROL, "ROL", 63, InternalOpKind.Rol };
        yield return new object[] { InstructionsEnum.ROR, "ROR", 64, InternalOpKind.Ror };
    }

    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { InstructionsEnum.ROL, 0x0000_0000_0000_0001UL, 1UL, 0x0000_0000_0000_0002UL };
        yield return new object[] { InstructionsEnum.ROL, 0x8000_0000_0000_0000UL, 1UL, 0x0000_0000_0000_0001UL };
        yield return new object[] { InstructionsEnum.ROL, 0x0123_4567_89AB_CDEFUL, 0UL, 0x0123_4567_89AB_CDEFUL };
        yield return new object[] { InstructionsEnum.ROL, 0x0123_4567_89AB_CDEFUL, 64UL, 0x0123_4567_89AB_CDEFUL };
        yield return new object[] { InstructionsEnum.ROL, 0x0123_4567_89AB_CDEFUL, 68UL, 0x1234_5678_9ABC_DEF0UL };
        yield return new object[] { InstructionsEnum.ROR, 0x0000_0000_0000_0001UL, 1UL, 0x8000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.ROR, 0x0000_0000_0000_0001UL, 65UL, 0x8000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.ROR, 0x0123_4567_89AB_CDEFUL, 0UL, 0x0123_4567_89AB_CDEFUL };
        yield return new object[] { InstructionsEnum.ROR, 0x0123_4567_89AB_CDEFUL, 4UL, 0xF012_3456_789A_BCDEUL };
    }

    [Theory]
    [MemberData(nameof(RotateOpcodeCases))]
    public void Rotate_OpcodeStatusAndCloseToRtlObjects_AreRuntimeClosed(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal((ushort)opcode, ResolveOpcodeValue(opcode));
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
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

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalExtensions);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);

        AssertCloseToRtlObject(opcode, mnemonic);
    }

    [Theory]
    [MemberData(nameof(RotateOpcodeCases))]
    public void Rotate_ClassifierRegistryAndMaterializer_PublishBinaryScalarAluMicroOp(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedKind)
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand, info.Value.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.False(info.Value.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.Equal(1, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext
            {
                OpCode = (uint)opcode,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = rs2,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)opcode,
                new DecoderContext
                {
                    OpCode = (uint)opcode,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = rs2,
                    HasImmediate = true,
                    Immediate = 1,
                }));

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(opcode, rd, rs1, rs2));
        Assert.Equal(expectedKind, internalOp.Kind);
        Assert.Equal(InternalOpDataType.DWord, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Theory]
    [MemberData(nameof(RotateOpcodeCases))]
    public void Rotate_DecoderIrAndProjector_RequireCanonicalBinaryRegisterPayload(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const byte rd = 8;
        const byte rs1 = 9;
        const byte rs2 = 10;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        VLIW_Instruction instruction = CreateScalarInstruction(
            opcode,
            rd: rd,
            rs1: rs1,
            rs2: rs2);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7200, bundleSerial: 80);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)opcode, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(rs2, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.Null(ir.VectorPayload);
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);

        VLIW_Instruction zeroShiftSource = CreateScalarInstruction(
            opcode,
            rd: rd,
            rs1: rs1,
            rs2: 0);
        decoder.DecodeInstructionBundle(CreateBundle(zeroShiftSource), 0x7220, 81);

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            opcode,
            rd: rd,
            rs1: rs1,
            rs2: rs2,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x7240, 82));
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void Rotate_ScalarAluAndCloseToRtlObjects_DefineXlen64RotateEdges(
        InstructionsEnum opcode,
        ulong source,
        ulong shiftSource,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)opcode,
            source,
            shiftSource,
            immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, ExecuteCloseToRtlObject(opcode, source, shiftSource));
    }

    [Theory]
    [InlineData(InstructionsEnum.ROL, 2, 0x8000_0000_0000_0000UL, 1UL, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.ROR, 3, 0x0000_0000_0000_0001UL, 1UL, 0x8000_0000_0000_0000UL)]
    public void Rotate_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        int vtId,
        ulong sourceValue,
        ulong shiftValue,
        ulong expectedResult)
    {
        const ulong pc = 0x7300UL;
        const ushort sourceRegister = 5;
        const ushort shiftRegister = 6;
        const ushort destinationRegister = 7;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, shiftRegister, shiftValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                rs2: (byte)shiftRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)opcode, decodeStatus.OpCode);
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

    [Theory]
    [InlineData(InstructionsEnum.ROL)]
    [InlineData(InstructionsEnum.ROR)]
    public void Rotate_WriteToX0_IsDiscardedAtRetire(InstructionsEnum opcode)
    {
        const int vtId = 1;
        const ulong pc = 0x7400UL;
        const ushort sourceRegister = 8;
        const ushort shiftRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0x8000_0000_0000_0000UL);
        core.WriteCommittedArch(vtId, shiftRegister, 1UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 0,
                rs1: (byte)sourceRegister,
                rs2: (byte)shiftRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0x8000_0000_0000_0000UL, core.ReadArch(vtId, sourceRegister));
        Assert.Equal(1UL, core.ReadArch(vtId, shiftRegister));
    }

    [Theory]
    [InlineData(InstructionsEnum.ROL, 0x8000_0000_0000_0000UL, 1UL, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.ROR, 0x0000_0000_0000_0001UL, 1UL, 0x8000_0000_0000_0000UL)]
    public void Rotate_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation(
        InstructionsEnum opcode,
        ulong sourceValue,
        ulong shiftValue,
        ulong expectedResult)
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);
        core.WriteCommittedArch(vtId, rs2, shiftValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(opcode, rd, rs1, rs2);

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

    [Theory]
    [InlineData(InstructionsEnum.ROL, 0x8000_0000_0000_0000UL, 1UL, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.ROR, 0x0000_0000_0000_0001UL, 1UL, 0x8000_0000_0000_0000UL)]
    public void Rotate_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        ulong sourceValue,
        ulong shiftValue,
        ulong expectedResult)
    {
        const int vtId = 2;
        const ulong pc = 0x7500UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, sourceValue);
        core.WriteCommittedArch(vtId, 6, shiftValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                opcode,
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

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void Rotate_RegisterCompilerHelpersOpenAlongsideImmediateRowsWithoutPopcntAliasOrImmediateAliases()
    {
        string[] closed =
        [
            "POPCNT"
        ];

        foreach (string mnemonic in closed)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.NotEqual(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.False(status.IsExecutableClaim, mnemonic);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic), mnemonic);
        }

        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.Contains("InstructionsEnum.ROL", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.ROR", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.ROLI", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.RORI", compilerSource, StringComparison.Ordinal);
        Assert.Contains("RotateLeftRegister", compilerSource, StringComparison.Ordinal);
        Assert.Contains("RotateRightRegister", compilerSource, StringComparison.Ordinal);
        Assert.Contains("RotateLeftByImmediate", compilerSource, StringComparison.Ordinal);
        Assert.Contains("RotateRightByImmediate", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RotateLeftImmediate", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RotateRightImmediate", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("void Rol(", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("void Ror(", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AppAsmFacade.Rol", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AppAsmFacade.Ror", compilerSource, StringComparison.Ordinal);
    }

    private static ushort ResolveOpcodeValue(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ROL => IsaOpcodeValues.ROL,
            InstructionsEnum.ROR => IsaOpcodeValues.ROR,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar rotate opcode.")
        };

    private static void AssertCloseToRtlObject(InstructionsEnum opcode, string mnemonic)
    {
        switch (opcode)
        {
            case InstructionsEnum.ROL:
                Assert.Equal(CloseToRtlRol.Mnemonic, mnemonic);
                Assert.Equal(64, CloseToRtlRol.XLen);
                Assert.Equal(0x3F, CloseToRtlRol.ShiftMask);
                Assert.Equal((ushort)opcode, CloseToRtlRol.Opcode);
                Assert.True(CloseToRtlRol.WritesScalarRegister);
                Assert.False(CloseToRtlRol.HasSideEffects);
                break;
            case InstructionsEnum.ROR:
                Assert.Equal(CloseToRtlRor.Mnemonic, mnemonic);
                Assert.Equal(64, CloseToRtlRor.XLen);
                Assert.Equal(0x3F, CloseToRtlRor.ShiftMask);
                Assert.Equal((ushort)opcode, CloseToRtlRor.Opcode);
                Assert.True(CloseToRtlRor.WritesScalarRegister);
                Assert.False(CloseToRtlRor.HasSideEffects);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar rotate opcode.");
        }
    }

    private static ulong ExecuteCloseToRtlObject(
        InstructionsEnum opcode,
        ulong source,
        ulong shiftSource) =>
        opcode switch
        {
            InstructionsEnum.ROL => CloseToRtlRol.Execute(source, shiftSource),
            InstructionsEnum.ROR => CloseToRtlRor.Execute(source, shiftSource),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar rotate opcode.")
        };

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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7600, bundleSerial: 83);
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
}
