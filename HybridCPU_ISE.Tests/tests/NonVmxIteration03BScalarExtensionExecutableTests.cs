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
using CloseToRtlSextB = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.Extension.SextBInstruction;
using CloseToRtlSextH = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.Extension.SextHInstruction;
using CloseToRtlZextH = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.Extension.ZextHInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class NonVmxIteration03BScalarExtensionExecutableTests
{
    public static IEnumerable<object[]> ExtensionOpcodeCases()
    {
        yield return new object[]
        {
            InstructionsEnum.SEXT_B, "SEXT.B", 60, InternalOpKind.SextB, InternalOpDataType.Byte
        };
        yield return new object[]
        {
            InstructionsEnum.SEXT_H, "SEXT.H", 61, InternalOpKind.SextH, InternalOpDataType.Half
        };
        yield return new object[]
        {
            InstructionsEnum.ZEXT_H, "ZEXT.H", 62, InternalOpKind.ZextH, InternalOpDataType.Half
        };
    }

    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { InstructionsEnum.SEXT_B, 0x0000_0000_0000_007FUL, 0x0000_0000_0000_007FUL };
        yield return new object[] { InstructionsEnum.SEXT_B, 0x0000_0000_0000_0080UL, 0xFFFF_FFFF_FFFF_FF80UL };
        yield return new object[] { InstructionsEnum.SEXT_B, 0xFFFF_FFFF_FFFF_01FFUL, 0xFFFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.SEXT_H, 0x0000_0000_0000_7FFFUL, 0x0000_0000_0000_7FFFUL };
        yield return new object[] { InstructionsEnum.SEXT_H, 0x0000_0000_0000_8000UL, 0xFFFF_FFFF_FFFF_8000UL };
        yield return new object[] { InstructionsEnum.SEXT_H, 0xFFFF_FFFF_0001_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.ZEXT_H, 0x0000_0000_0000_FFFFUL, 0x0000_0000_0000_FFFFUL };
        yield return new object[] { InstructionsEnum.ZEXT_H, 0xFFFF_FFFF_0001_8000UL, 0x0000_0000_0000_8000UL };
    }

    [Theory]
    [MemberData(nameof(ExtensionOpcodeCases))]
    public void ScalarExtension_OpcodeStatusAndCloseToRtlObjects_AreRuntimeClosed(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind,
        InternalOpDataType expectedDataType)
    {
        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal((ushort)opcode, ResolveOpcodeValue(opcode));
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));
        Assert.Equal(
            expectedDataType,
            new InternalOpBuilder().Build(CreateInstructionIr(opcode, rd: 1, rs1: 2)).DataType);

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
    [MemberData(nameof(ExtensionOpcodeCases))]
    public void ScalarExtension_ClassifierRegistryAndMaterializer_PublishUnaryScalarAluMicroOp(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedKind,
        InternalOpDataType expectedDataType)
    {
        const byte rd = 7;
        const byte rs1 = 5;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Value.Category);
        Assert.Equal(1, info.Value.OperandCount);
        Assert.Equal(InstructionFlags.None, info.Value.Flags);
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
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { (int)rs1 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(opcode, rd, rs1));
        Assert.Equal(expectedKind, internalOp.Kind);
        Assert.Equal(expectedDataType, internalOp.DataType);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
        if (opcode is InstructionsEnum.SEXT_B or InstructionsEnum.SEXT_H)
        {
            Assert.True(internalOp.Flags.HasFlag(InternalOpFlags.Signed));
        }
        else
        {
            Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        }
    }

    [Theory]
    [MemberData(nameof(ExtensionOpcodeCases))]
    public void ScalarExtension_DecoderIrAndProjector_RequireCanonicalUnaryRegisterPayload(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind,
        InternalOpDataType expectedDataType)
    {
        const byte rd = 8;
        const byte rs1 = 9;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        VLIW_Instruction instruction = CreateScalarInstruction(
            opcode,
            rd: rd,
            rs1: rs1);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x6B00, bundleSerial: 70);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)opcode, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.Null(ir.VectorPayload);
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));
        Assert.Equal(expectedDataType, new InternalOpBuilder().Build(ir).DataType);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);

        VLIW_Instruction registerAlias = CreateScalarInstruction(
            opcode,
            rd: rd,
            rs1: rs1,
            rs2: 3);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(registerAlias), 0x6B20, 71));

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            opcode,
            rd: rd,
            rs1: rs1,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x6B40, 72));
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void ScalarExtension_ScalarAluAndCloseToRtlObjects_DefineLowFieldExtensionEdges(
        InstructionsEnum opcode,
        ulong source,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)opcode,
            source,
            op2: 0,
            immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, ExecuteCloseToRtlObject(opcode, source));
    }

    [Theory]
    [InlineData(InstructionsEnum.SEXT_B, 2, 0x0000_0000_0000_0080UL, 0xFFFF_FFFF_FFFF_FF80UL)]
    [InlineData(InstructionsEnum.SEXT_H, 3, 0x0000_0000_0000_8000UL, 0xFFFF_FFFF_FFFF_8000UL)]
    [InlineData(InstructionsEnum.ZEXT_H, 2, 0xFFFF_FFFF_0000_8000UL, 0x0000_0000_0000_8000UL)]
    public void ScalarExtension_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        int vtId,
        ulong sourceValue,
        ulong expectedResult)
    {
        const ulong pc = 0x6C00UL;
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
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister));

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
    [InlineData(InstructionsEnum.SEXT_B)]
    [InlineData(InstructionsEnum.SEXT_H)]
    [InlineData(InstructionsEnum.ZEXT_H)]
    public void ScalarExtension_WriteToX0_IsDiscardedAtRetire(InstructionsEnum opcode)
    {
        const int vtId = 1;
        const ulong pc = 0x6D00UL;
        const ushort sourceRegister = 8;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0xFFFF_FFFF_FFFF_8000UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 0,
                rs1: (byte)sourceRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0xFFFF_FFFF_FFFF_8000UL, core.ReadArch(vtId, sourceRegister));
    }

    [Theory]
    [InlineData(InstructionsEnum.SEXT_B, 0x0000_0000_0000_0080UL, 0xFFFF_FFFF_FFFF_FF80UL)]
    [InlineData(InstructionsEnum.SEXT_H, 0x0000_0000_0000_8000UL, 0xFFFF_FFFF_FFFF_8000UL)]
    [InlineData(InstructionsEnum.ZEXT_H, 0xFFFF_FFFF_0000_8000UL, 0x0000_0000_0000_8000UL)]
    public void ScalarExtension_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation(
        InstructionsEnum opcode,
        ulong sourceValue,
        ulong expectedResult)
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(opcode, rd, rs1);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 97,
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
            bundleSerial: 97,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Theory]
    [InlineData(InstructionsEnum.SEXT_B, 0x0000_0000_0000_0080UL, 0xFFFF_FFFF_FFFF_FF80UL)]
    [InlineData(InstructionsEnum.SEXT_H, 0x0000_0000_0000_8000UL, 0xFFFF_FFFF_FFFF_8000UL)]
    [InlineData(InstructionsEnum.ZEXT_H, 0xFFFF_FFFF_0000_8000UL, 0x0000_0000_0000_8000UL)]
    public void ScalarExtension_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        ulong sourceValue,
        ulong expectedResult)
    {
        const int vtId = 2;
        const ulong pc = 0x6E00UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, sourceValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                opcode,
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

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void ScalarExtension_CompilerHelpersOpenWithoutPopcntAlias()
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
        Assert.Contains("InstructionsEnum.SEXT_B", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.SEXT_H", compilerSource, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.ZEXT_H", compilerSource, StringComparison.Ordinal);
        Assert.Contains("SignExtendByte", compilerSource, StringComparison.Ordinal);
        Assert.Contains("SignExtendHalf", compilerSource, StringComparison.Ordinal);
        Assert.Contains("ZeroExtendHalf", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("POPCNT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Popcnt", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PopulationCount", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CountPopulation", compilerSource, StringComparison.Ordinal);
    }

    private static ushort ResolveOpcodeValue(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.SEXT_B => IsaOpcodeValues.SEXT_B,
            InstructionsEnum.SEXT_H => IsaOpcodeValues.SEXT_H,
            InstructionsEnum.ZEXT_H => IsaOpcodeValues.ZEXT_H,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar extension opcode.")
        };

    private static void AssertCloseToRtlObject(InstructionsEnum opcode, string mnemonic)
    {
        switch (opcode)
        {
            case InstructionsEnum.SEXT_B:
                Assert.Equal(CloseToRtlSextB.Mnemonic, mnemonic);
                Assert.Equal(8, CloseToRtlSextB.SourceBits);
                Assert.Equal(64, CloseToRtlSextB.XLen);
                Assert.Equal((ushort)opcode, CloseToRtlSextB.Opcode);
                Assert.True(CloseToRtlSextB.WritesScalarRegister);
                Assert.False(CloseToRtlSextB.HasSideEffects);
                break;
            case InstructionsEnum.SEXT_H:
                Assert.Equal(CloseToRtlSextH.Mnemonic, mnemonic);
                Assert.Equal(16, CloseToRtlSextH.SourceBits);
                Assert.Equal(64, CloseToRtlSextH.XLen);
                Assert.Equal((ushort)opcode, CloseToRtlSextH.Opcode);
                Assert.True(CloseToRtlSextH.WritesScalarRegister);
                Assert.False(CloseToRtlSextH.HasSideEffects);
                break;
            case InstructionsEnum.ZEXT_H:
                Assert.Equal(CloseToRtlZextH.Mnemonic, mnemonic);
                Assert.Equal(16, CloseToRtlZextH.SourceBits);
                Assert.Equal(64, CloseToRtlZextH.XLen);
                Assert.Equal((ushort)opcode, CloseToRtlZextH.Opcode);
                Assert.True(CloseToRtlZextH.WritesScalarRegister);
                Assert.False(CloseToRtlZextH.HasSideEffects);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar extension opcode.");
        }
    }

    private static ulong ExecuteCloseToRtlObject(InstructionsEnum opcode, ulong source) =>
        opcode switch
        {
            InstructionsEnum.SEXT_B => CloseToRtlSextB.Execute(source),
            InstructionsEnum.SEXT_H => CloseToRtlSextH.Execute(source),
            InstructionsEnum.ZEXT_H => CloseToRtlZextH.Execute(source),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unexpected scalar extension opcode.")
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x6F00, bundleSerial: 73);
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
