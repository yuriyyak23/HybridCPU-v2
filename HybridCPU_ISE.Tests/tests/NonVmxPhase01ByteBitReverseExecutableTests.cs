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
using CloseToRtlBrev8 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.ByteBitReverse.Brev8Instruction;
using CloseToRtlRev8 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.ByteBitReverse.Rev8Instruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxPhase01ByteBitReverseExecutableTests
{
    public static IEnumerable<object[]> ByteBitReverseOpcodeCases()
    {
        yield return new object[] { InstructionsEnum.REV8, "REV8", 331, InternalOpKind.Rev8 };
        yield return new object[] { InstructionsEnum.BREV8, "BREV8", 332, InternalOpKind.Brev8 };
    }

    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { InstructionsEnum.REV8, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.REV8, 0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.REV8, 0x0123_4567_89AB_CDEFUL, 0xEFCD_AB89_6745_2301UL };
        yield return new object[] { InstructionsEnum.REV8, 0x8000_0000_0000_0001UL, 0x0100_0000_0000_0080UL };
        yield return new object[] { InstructionsEnum.BREV8, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.BREV8, 0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.BREV8, 0x0123_4567_89AB_CDEFUL, 0x80C4_A2E6_91D5_B3F7UL };
        yield return new object[] { InstructionsEnum.BREV8, 0x8040_2010_0804_0201UL, 0x0102_0408_1020_4080UL };
    }

    [Theory]
    [MemberData(nameof(ByteBitReverseOpcodeCases))]
    public void ByteBitReverse_OpcodeStatusAndCloseToRtlObjects_AreRuntimeClosed(
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
    [MemberData(nameof(ByteBitReverseOpcodeCases))]
    public void ByteBitReverse_ClassifierRegistryAndMaterializer_PublishUnaryScalarAluMicroOp(
        InstructionsEnum opcode,
        string mnemonic,
        int _,
        InternalOpKind expectedKind)
    {
        const byte rd = 7;
        const byte rs1 = 5;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Value.Category);
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
        Assert.False(scalar.UsesImmediate);
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
                    Reg3ID = 1,
                    HasImmediate = true,
                    Immediate = 0,
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
                    Immediate = 1,
                }));

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(opcode, rd, rs1));
        Assert.Equal(expectedKind, internalOp.Kind);
        Assert.Equal(InternalOpDataType.DWord, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Theory]
    [MemberData(nameof(ByteBitReverseOpcodeCases))]
    public void ByteBitReverse_EncoderDecoderIrAndProjector_RequireCanonicalUnaryRegisterPayload(
        InstructionsEnum opcode,
        string mnemonic,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const byte rd = 8;
        const byte rs1 = 9;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        VLIW_Instruction instruction = CreateScalarInstruction(opcode, rd, rs1);
        Assert.Equal((uint)opcode, instruction.OpCode);
        Assert.Equal(0, instruction.Reg3ID);
        Assert.Equal(0, instruction.Immediate);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0xB200, bundleSerial: 420);

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

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)opcode, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(new[] { (int)rs1 }, scalar.ReadRegisters);

        VLIW_Instruction registerAlias = CreateScalarInstruction(opcode, rd, rs1, rs2: 10);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(registerAlias), 0xB240, 421));

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            opcode,
            rd,
            rs1,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0xB260, 422));
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void ByteBitReverse_ScalarAluCloseToRtlAndGoldenVectors_DefineXlen64Ordering(
        InstructionsEnum opcode,
        ulong value,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute((uint)opcode, value, op2: 0, immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, CloseToRtlExecute(opcode, value));
        Assert.Equal(expected, CloseToRtlEvaluate(opcode, value));

        foreach ((ulong vectorValue, ulong vectorResult) in GetLocalGoldenVectors(opcode))
        {
            Assert.Equal(
                vectorResult,
                ScalarAluOps.Compute((uint)opcode, vectorValue, op2: 0, immediate: 0));
            Assert.Equal(vectorResult, CloseToRtlExecute(opcode, vectorValue));
        }
    }

    [Theory]
    [MemberData(nameof(ByteBitReverseOpcodeCases))]
    public void ByteBitReverse_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const int vtId = 2;
        const ulong pc = 0xB300UL;
        const ushort sourceRegister = 5;
        const ushort destinationRegister = 7;
        (ulong sourceValue, ulong expectedResult) = GetPipelineValues(opcode);
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

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
    [MemberData(nameof(ByteBitReverseOpcodeCases))]
    public void ByteBitReverse_WriteToX0_IsDiscardedAtRetire(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const int vtId = 1;
        const ulong pc = 0xB400UL;
        const ushort sourceRegister = 8;
        (ulong sourceValue, ulong _) = GetPipelineValues(opcode);

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 0,
                rs1: (byte)sourceRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(sourceValue, core.ReadArch(vtId, sourceRegister));
    }

    [Theory]
    [MemberData(nameof(ByteBitReverseOpcodeCases))]
    public void ByteBitReverse_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        (ulong sourceValue, ulong expectedResult) = GetPipelineValues(opcode);

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

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
                bundleSerial: 428,
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
            bundleSerial: 428,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Theory]
    [MemberData(nameof(ByteBitReverseOpcodeCases))]
    public void ByteBitReverse_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const int vtId = 2;
        const ulong pc = 0xB500UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
        (ulong sourceValue, ulong expectedResult) = GetPipelineValues(opcode);

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

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
    public void ByteBitReverse_CompilerHelpersOpenWithoutVmxOrAliasHelpers()
    {
        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        string vmxSource = ReadAllSource(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_ISE", "Core", "VMX"));

        foreach ((InstructionsEnum opcode, string mnemonic, string helperName, string aliasFragment) in new[]
        {
            (InstructionsEnum.REV8, "REV8", "ReverseByteOrder", "Rev8"),
            (InstructionsEnum.BREV8, "BREV8", "ReverseBitsInEachByte", "Brev8"),
        })
        {
            Assert.Contains($"InstructionsEnum.{mnemonic}", compilerSource, StringComparison.Ordinal);
            Assert.Contains(helperName, compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain(aliasFragment, compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("ReverseBytes", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("ReverseBitsInByte", compilerSource, StringComparison.Ordinal);

            Assert.DoesNotContain($"InstructionsEnum.{mnemonic}", vmxSource, StringComparison.Ordinal);

            OpcodeInfo info = OpcodeRegistry.GetInfo((uint)opcode)!.Value;
            Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
            Assert.NotEqual(InstructionClass.Vmx, info.InstructionClass);
        }
    }

    private static void AssertCloseToRtlObject(
        InstructionsEnum opcode,
        string mnemonic)
    {
        Assert.Equal(mnemonic, CloseToRtlMnemonic(opcode));
        Assert.Equal("rd, rs1", CloseToRtlOperandShape(opcode));
        Assert.Equal("ExecutableScalarAlu", CloseToRtlEvidenceBoundary(opcode));
        Assert.Equal(64, CloseToRtlXLen(opcode));
        Assert.Equal((ushort)opcode, CloseToRtlOpcode(opcode));
        Assert.True(CloseToRtlHasOpcodeAllocation(opcode));
        Assert.True(CloseToRtlIsExecutable(opcode));
        Assert.True(CloseToRtlWritesScalarRegister(opcode));
        Assert.False(CloseToRtlHasSideEffects(opcode));
        Assert.False(CloseToRtlCompilerHelperAllowed(opcode));
        Assert.False(CloseToRtlRequiresVmxProjection(opcode));
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0xB600, bundleSerial: 429);
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
            Rs2 = 0,
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
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.UINT64,
            rd,
            rs1,
            rs2,
            predicateMask: 0xFF,
            immediate: immediate);
        instruction.VirtualThreadId = virtualThreadId;
        return instruction;
    }

    private static ushort ResolveOpcodeValue(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => IsaOpcodeValues.REV8,
            InstructionsEnum.BREV8 => IsaOpcodeValues.BREV8,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static (ulong Source, ulong Expected) GetPipelineValues(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => (0x0123_4567_89AB_CDEFUL, 0xEFCD_AB89_6745_2301UL),
            InstructionsEnum.BREV8 => (0x0123_4567_89AB_CDEFUL, 0x80C4_A2E6_91D5_B3F7UL),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static string CloseToRtlMnemonic(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.Mnemonic,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.Mnemonic,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static string CloseToRtlOperandShape(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.OperandShape,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.OperandShape,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static string CloseToRtlEvidenceBoundary(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.EvidenceBoundary,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.EvidenceBoundary,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static int CloseToRtlXLen(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.XLen,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.XLen,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static ushort CloseToRtlOpcode(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.Opcode,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.Opcode,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlHasOpcodeAllocation(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.HasOpcodeAllocation,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.HasOpcodeAllocation,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlIsExecutable(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.IsExecutable,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.IsExecutable,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlWritesScalarRegister(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.WritesScalarRegister,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.WritesScalarRegister,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlHasSideEffects(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.HasSideEffects,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.HasSideEffects,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlCompilerHelperAllowed(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.CompilerHelperAllowed,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.CompilerHelperAllowed,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlRequiresVmxProjection(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.RequiresVmxProjection,
            InstructionsEnum.BREV8 => CloseToRtlBrev8.RequiresVmxProjection,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static ulong CloseToRtlExecute(InstructionsEnum opcode, ulong value) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.Execute(value),
            InstructionsEnum.BREV8 => CloseToRtlBrev8.Execute(value),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static ulong CloseToRtlEvaluate(InstructionsEnum opcode, ulong value) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.EvaluateXLen64(value),
            InstructionsEnum.BREV8 => CloseToRtlBrev8.EvaluateXLen64(value),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static IEnumerable<(ulong Value, ulong Result)> GetLocalGoldenVectors(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.REV8 => CloseToRtlRev8.GetLocalGoldenVectors()
                .Select(vector => (vector.Value, vector.Result)),
            InstructionsEnum.BREV8 => CloseToRtlBrev8.GetLocalGoldenVectors()
                .Select(vector => (vector.Value, vector.Result)),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static string ReadAllSource(string root)
    {
        if (!Directory.Exists(root))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
