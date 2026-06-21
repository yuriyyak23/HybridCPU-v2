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
using CloseToRtlRoli = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.Rotates.RoliInstruction;
using CloseToRtlRori = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.Rotates.RoriInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class NonVmxPhase02RotateImmediateExecutableTests
{
    public static IEnumerable<object[]> RotateImmediateOpcodeCases()
    {
        yield return new object[] { InstructionsEnum.ROLI, "ROLI", 335, InternalOpKind.RolI };
        yield return new object[] { InstructionsEnum.RORI, "RORI", 336, InternalOpKind.RorI };
    }

    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { InstructionsEnum.ROLI, 0x0000_0000_0000_0001UL, (ushort)1, 0x0000_0000_0000_0002UL };
        yield return new object[] { InstructionsEnum.ROLI, 0x8000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0001UL };
        yield return new object[] { InstructionsEnum.ROLI, 0x0123_4567_89AB_CDEFUL, (ushort)0, 0x0123_4567_89AB_CDEFUL };
        yield return new object[] { InstructionsEnum.ROLI, 0x0123_4567_89AB_CDEFUL, (ushort)4, 0x1234_5678_9ABC_DEF0UL };
        yield return new object[] { InstructionsEnum.ROLI, 0x0000_0000_0000_0001UL, (ushort)63, 0x8000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.RORI, 0x0000_0000_0000_0001UL, (ushort)1, 0x8000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.RORI, 0x0000_0000_0000_0001UL, (ushort)63, 0x0000_0000_0000_0002UL };
        yield return new object[] { InstructionsEnum.RORI, 0x0123_4567_89AB_CDEFUL, (ushort)0, 0x0123_4567_89AB_CDEFUL };
        yield return new object[] { InstructionsEnum.RORI, 0x0123_4567_89AB_CDEFUL, (ushort)4, 0xF012_3456_789A_BCDEUL };
    }

    [Theory]
    [MemberData(nameof(RotateImmediateOpcodeCases))]
    public void RotateImmediate_OpcodeStatusAndCloseToRtlObjects_AreRuntimeClosed(
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
    [MemberData(nameof(RotateImmediateOpcodeCases))]
    public void RotateImmediate_ClassifierRegistryAndMaterializer_PublishImmediateScalarAluMicroOp(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedKind)
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const ushort imm6 = 4;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
        Assert.Equal(2, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.UsesImmediate, info.Value.Flags);
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
                Reg3ID = 0,
                HasImmediate = true,
                Immediate = imm6,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);
        Assert.True(scalar.UsesImmediate);
        Assert.Equal(imm6, scalar.Immediate);
        Assert.Equal(new[] { (int)rs1 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)opcode,
                new DecoderContext
                {
                    OpCode = (uint)opcode,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = 3,
                    HasImmediate = true,
                    Immediate = imm6,
                }));

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)opcode,
                new DecoderContext
                {
                    OpCode = (uint)opcode,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = 0,
                    HasImmediate = true,
                    Immediate = 64,
                }));

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(opcode, rd, rs1, imm6));
        Assert.Equal(expectedKind, internalOp.Kind);
        Assert.Equal(rs1, internalOp.Rs1);
        Assert.Equal(0, internalOp.Rs2);
        Assert.Equal(imm6, internalOp.Immediate);
        Assert.Equal(InternalOpDataType.DWord, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Theory]
    [MemberData(nameof(RotateImmediateOpcodeCases))]
    public void RotateImmediate_DecoderIrAndProjector_RequireCanonicalImm6Payload(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const byte rd = 8;
        const byte rs1 = 9;
        const ushort imm6 = 4;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        VLIW_Instruction instruction = CreateScalarRotateImmediateInstruction(
            opcode,
            rd: rd,
            rs1: rs1,
            immediate6: imm6);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7800, bundleSerial: 100);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)opcode, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(imm6, ir.Imm);
        Assert.Null(ir.VectorPayload);
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);
        Assert.True(scalar.UsesImmediate);
        Assert.Equal(imm6, scalar.Immediate);

        VLIW_Instruction registerAlias = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            3,
            immediate: imm6);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(registerAlias), 0x7820, 101));

        VLIW_Instruction outOfRange = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            0,
            immediate: 64);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(outOfRange), 0x7840, 102));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeScalarRotateImmediate(
                (uint)opcode,
                DataTypeEnum.UINT64,
                rd,
                rs1,
                immediate6: 64));
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void RotateImmediate_ScalarAluCloseToRtlAndGoldenVectors_DefineImm6Edges(
        InstructionsEnum opcode,
        ulong source,
        ushort immediate6,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)opcode,
            source,
            op2: 0,
            immediate: immediate6);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, ExecuteCloseToRtlObject(opcode, source, immediate6));

        foreach ((ulong goldenSource, ushort goldenImmediate, ulong goldenExpected) in GetGoldenVectors(opcode))
        {
            Assert.Equal(
                goldenExpected,
                ScalarAluOps.Compute((uint)opcode, goldenSource, op2: 0, immediate: goldenImmediate));
            Assert.Equal(goldenExpected, ExecuteCloseToRtlObject(opcode, goldenSource, goldenImmediate));
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.ROLI, 2, 0x8000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.RORI, 3, 0x0000_0000_0000_0001UL, (ushort)1, 0x8000_0000_0000_0000UL)]
    public void RotateImmediate_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        int vtId,
        ulong sourceValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const ulong pc = 0x7900UL;
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
            CreateBundle(CreateScalarRotateImmediateInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                immediate6: immediate6));

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
    [InlineData(InstructionsEnum.ROLI)]
    [InlineData(InstructionsEnum.RORI)]
    public void RotateImmediate_WriteToX0_IsDiscardedAtRetire(InstructionsEnum opcode)
    {
        const int vtId = 1;
        const ulong pc = 0x7A00UL;
        const ushort sourceRegister = 8;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0x8000_0000_0000_0000UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarRotateImmediateInstruction(
                opcode,
                rd: 0,
                rs1: (byte)sourceRegister,
                immediate6: 1));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0x8000_0000_0000_0000UL, core.ReadArch(vtId, sourceRegister));
    }

    [Theory]
    [InlineData(InstructionsEnum.ROLI, 0x8000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.RORI, 0x0000_0000_0000_0001UL, (ushort)1, 0x8000_0000_0000_0000UL)]
    public void RotateImmediate_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation(
        InstructionsEnum opcode,
        ulong sourceValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(opcode, rd, rs1, immediate6);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 108,
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
            bundleSerial: 108,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Theory]
    [InlineData(InstructionsEnum.ROLI, 0x8000_0000_0000_0000UL, (ushort)1, 0x0000_0000_0000_0001UL)]
    [InlineData(InstructionsEnum.RORI, 0x0000_0000_0000_0001UL, (ushort)1, 0x8000_0000_0000_0000UL)]
    public void RotateImmediate_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        ulong sourceValue,
        ushort immediate6,
        ulong expectedResult)
    {
        const int vtId = 2;
        const ulong pc = 0x7B00UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, sourceValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarRotateImmediateInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: 5,
                immediate6: immediate6,
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
    public void RotateImmediate_CompilerHelpersOpenWithoutAliasesOrVmxSpecificPath()
    {
        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.Contains("InstructionsEnum.ROLI", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.RORI", compilerSource, StringComparison.Ordinal);
        Assert.Contains("RotateLeftByImmediate", compilerSource, StringComparison.Ordinal);
        Assert.Contains("RotateRightByImmediate", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RotateLeftImmediate", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RotateRightImmediate", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Roli", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Rori", compilerSource, StringComparison.Ordinal);

        Assert.False(CloseToRtlRoli.RequiresVmxProjection);
        Assert.False(CloseToRtlRori.RequiresVmxProjection);
    }

    private static ushort ResolveOpcodeValue(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ROLI => IsaOpcodeValues.ROLI,
            InstructionsEnum.RORI => IsaOpcodeValues.RORI,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar rotate-immediate opcode.")
        };

    private static void AssertCloseToRtlObject(InstructionsEnum opcode, string mnemonic)
    {
        switch (opcode)
        {
            case InstructionsEnum.ROLI:
                Assert.Equal(CloseToRtlRoli.Mnemonic, mnemonic);
                Assert.Equal("ExecutableScalarAlu", CloseToRtlRoli.EvidenceBoundary);
                Assert.Equal(64, CloseToRtlRoli.XLen);
                Assert.Equal(6, CloseToRtlRoli.ImmediateBits);
                Assert.Equal(0x3F, CloseToRtlRoli.ImmediateMask);
                Assert.Equal((ushort)opcode, CloseToRtlRoli.Opcode);
                Assert.True(CloseToRtlRoli.HasOpcodeAllocation);
                Assert.True(CloseToRtlRoli.IsExecutable);
                Assert.True(CloseToRtlRoli.WritesScalarRegister);
                Assert.False(CloseToRtlRoli.HasSideEffects);
                Assert.False(CloseToRtlRoli.CompilerHelperAllowed);
                break;
            case InstructionsEnum.RORI:
                Assert.Equal(CloseToRtlRori.Mnemonic, mnemonic);
                Assert.Equal("ExecutableScalarAlu", CloseToRtlRori.EvidenceBoundary);
                Assert.Equal(64, CloseToRtlRori.XLen);
                Assert.Equal(6, CloseToRtlRori.ImmediateBits);
                Assert.Equal(0x3F, CloseToRtlRori.ImmediateMask);
                Assert.Equal((ushort)opcode, CloseToRtlRori.Opcode);
                Assert.True(CloseToRtlRori.HasOpcodeAllocation);
                Assert.True(CloseToRtlRori.IsExecutable);
                Assert.True(CloseToRtlRori.WritesScalarRegister);
                Assert.False(CloseToRtlRori.HasSideEffects);
                Assert.False(CloseToRtlRori.CompilerHelperAllowed);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar rotate-immediate opcode.");
        }
    }

    private static ulong ExecuteCloseToRtlObject(
        InstructionsEnum opcode,
        ulong source,
        ushort immediate6) =>
        opcode switch
        {
            InstructionsEnum.ROLI => CloseToRtlRoli.Execute(source, immediate6),
            InstructionsEnum.RORI => CloseToRtlRori.Execute(source, immediate6),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar rotate-immediate opcode.")
        };

    private static IEnumerable<(ulong Source, ushort Immediate6, ulong Expected)> GetGoldenVectors(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.ROLI => CloseToRtlRoli.GetLocalGoldenVectors()
                .Select(static vector => (vector.Source, vector.Immediate6, vector.Expected)),
            InstructionsEnum.RORI => CloseToRtlRori.GetLocalGoldenVectors()
                .Select(static vector => (vector.Source, vector.Immediate6, vector.Expected)),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar rotate-immediate opcode.")
        };
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7C00, bundleSerial: 109);
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
        ushort immediate6)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = rd,
            Rs1 = rs1,
            Rs2 = 0,
            Imm = immediate6
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

    private static VLIW_Instruction CreateScalarRotateImmediateInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        ushort immediate6 = 0,
        byte virtualThreadId = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalarRotateImmediate(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            immediate6,
            predicateMask: 0xFF);
        instruction.StreamLength = 0;
        instruction.VirtualThreadId = virtualThreadId;
        return instruction;
    }
}
