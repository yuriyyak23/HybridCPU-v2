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
using CloseToRtlOrn = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BooleanInvert.OrnInstruction;
using CloseToRtlXnor = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BooleanInvert.XnorInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxPhase01BooleanInvertExecutableTests
{
    public static IEnumerable<object[]> BooleanInvertOpcodeCases()
    {
        yield return new object[] { InstructionsEnum.ORN, "ORN", 66, InternalOpKind.OrN };
        yield return new object[] { InstructionsEnum.XNOR, "XNOR", 67, InternalOpKind.Xnor };
    }

    public static IEnumerable<object[]> ExecutionCases()
    {
        yield return new object[] { InstructionsEnum.ORN, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.ORN, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.ORN, 0x1234_5678_9ABC_DEF0UL, 0x00FF_00FF_00FF_00FFUL, 0xFF34_FF78_FFBC_FFF0UL };
        yield return new object[] { InstructionsEnum.XNOR, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL };
        yield return new object[] { InstructionsEnum.XNOR, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL };
        yield return new object[] { InstructionsEnum.XNOR, 0xAAAA_AAAA_5555_5555UL, 0x0F0F_0F0F_F0F0_F0F0UL, 0x5A5A_5A5A_5A5A_5A5AUL };
    }

    [Theory]
    [MemberData(nameof(BooleanInvertOpcodeCases))]
    public void BooleanInvert_OpcodeStatusAndCloseToRtlObjects_AreRuntimeClosed(
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
    [MemberData(nameof(BooleanInvertOpcodeCases))]
    public void BooleanInvert_ClassifierRegistryAndMaterializer_PublishBinaryScalarAluMicroOp(
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
    [MemberData(nameof(BooleanInvertOpcodeCases))]
    public void BooleanInvert_EncoderDecoderIrAndProjector_RequireCanonicalBinaryRegisterPayload(
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

        VLIW_Instruction instruction = CreateScalarInstruction(opcode, rd, rs1, rs2);
        Assert.Equal((uint)opcode, instruction.OpCode);
        Assert.Equal(0, instruction.Immediate);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x9200, bundleSerial: 220);

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

        decoder.DecodeInstructionBundle(CreateBundle(CreateScalarInstruction(opcode, rd, rs1, rs2: 0)), 0x9220, 221);

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            opcode,
            rd,
            rs1,
            rs2,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0x9240, 222));
    }

    [Theory]
    [MemberData(nameof(ExecutionCases))]
    public void BooleanInvert_ScalarAluCloseToRtlAndGoldenVectors_DefineXlen64Edges(
        InstructionsEnum opcode,
        ulong left,
        ulong right,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute((uint)opcode, left, right, immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, CloseToRtlExecute(opcode, left, right));
        Assert.Equal(expected, CloseToRtlEvaluate(opcode, left, right));

        foreach ((ulong vectorLeft, ulong vectorRight, ulong vectorResult) in GetLocalGoldenVectors(opcode))
        {
            Assert.Equal(
                vectorResult,
                ScalarAluOps.Compute((uint)opcode, vectorLeft, vectorRight, immediate: 0));
            Assert.Equal(vectorResult, CloseToRtlExecute(opcode, vectorLeft, vectorRight));
        }
    }

    [Theory]
    [MemberData(nameof(BooleanInvertOpcodeCases))]
    public void BooleanInvert_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const int vtId = 2;
        const ulong pc = 0x9300UL;
        const ushort sourceRegister = 5;
        const ushort maskRegister = 6;
        const ushort destinationRegister = 7;
        (ulong sourceValue, ulong maskValue, ulong expectedResult) = GetPipelineValues(opcode);
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, maskRegister, maskValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                rs2: (byte)maskRegister));

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
    [MemberData(nameof(BooleanInvertOpcodeCases))]
    public void BooleanInvert_WriteToX0_IsDiscardedAtRetire(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const int vtId = 1;
        const ulong pc = 0x9400UL;
        const ushort sourceRegister = 8;
        const ushort maskRegister = 9;

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0xFFFF_FFFF_FFFF_FFFFUL);
        core.WriteCommittedArch(vtId, maskRegister, 0UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                opcode,
                rd: 0,
                rs1: (byte)sourceRegister,
                rs2: (byte)maskRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        Assert.Equal(0xFFFF_FFFF_FFFF_FFFFUL, core.ReadArch(vtId, sourceRegister));
        Assert.Equal(0UL, core.ReadArch(vtId, maskRegister));
    }

    [Theory]
    [MemberData(nameof(BooleanInvertOpcodeCases))]
    public void BooleanInvert_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;
        (ulong sourceValue, ulong maskValue, ulong expectedResult) = GetPipelineValues(opcode);

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);
        core.WriteCommittedArch(vtId, rs2, maskValue);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(opcode, rd, rs1, rs2);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 228,
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
            bundleSerial: 228,
            vtId);

        Assert.Equal(expectedResult, core.ReadArch(vtId, rd));
    }

    [Theory]
    [MemberData(nameof(BooleanInvertOpcodeCases))]
    public void BooleanInvert_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth(
        InstructionsEnum opcode,
        string _,
        int expectedOpcode,
        InternalOpKind expectedKind)
    {
        const int vtId = 2;
        const ulong pc = 0x9500UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
        (ulong sourceValue, ulong maskValue, ulong expectedResult) = GetPipelineValues(opcode);

        Assert.Equal(expectedOpcode, (int)opcode);
        Assert.Equal(expectedKind, InternalOpBuilder.MapToKind(ResolveOpcodeValue(opcode)));

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, sourceValue);
        core.WriteCommittedArch(vtId, 6, maskValue);
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
    public void BooleanInvert_CompilerHelpersOpenWithoutAliasesOrVmxSpecificPath()
    {
        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        string vmxSource = ReadAllSource(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_ISE", "Core", "VMX"));

        foreach ((InstructionsEnum opcode, string mnemonic, string helperName, string helperFragment) in new[]
        {
            (InstructionsEnum.ORN, "ORN", "OrWithInvertedSecond", "Orn"),
            (InstructionsEnum.XNOR, "XNOR", "ExclusiveNor", "Xnor"),
        })
        {
            Assert.Contains($"InstructionsEnum.{mnemonic}", compilerSource, StringComparison.Ordinal);
            Assert.Contains(helperName, compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain(helperFragment, compilerSource, StringComparison.Ordinal);

            Assert.DoesNotContain($"InstructionsEnum.{mnemonic}", vmxSource, StringComparison.Ordinal);
            Assert.DoesNotContain(mnemonic, vmxSource, StringComparison.Ordinal);

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
        Assert.Equal("rd, rs1, rs2", CloseToRtlOperandShape(opcode));
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x9600, bundleSerial: 229);
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
            InstructionsEnum.ORN => IsaOpcodeValues.ORN,
            InstructionsEnum.XNOR => IsaOpcodeValues.XNOR,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static (ulong Source, ulong Mask, ulong Expected) GetPipelineValues(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => (0x1234_5678_9ABC_DEF0UL, 0x00FF_00FF_00FF_00FFUL, 0xFF34_FF78_FFBC_FFF0UL),
            InstructionsEnum.XNOR => (0xAAAA_AAAA_5555_5555UL, 0x0F0F_0F0F_F0F0_F0F0UL, 0x5A5A_5A5A_5A5A_5A5AUL),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static string CloseToRtlMnemonic(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.Mnemonic,
            InstructionsEnum.XNOR => CloseToRtlXnor.Mnemonic,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static string CloseToRtlOperandShape(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.OperandShape,
            InstructionsEnum.XNOR => CloseToRtlXnor.OperandShape,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static string CloseToRtlEvidenceBoundary(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.EvidenceBoundary,
            InstructionsEnum.XNOR => CloseToRtlXnor.EvidenceBoundary,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static int CloseToRtlXLen(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.XLen,
            InstructionsEnum.XNOR => CloseToRtlXnor.XLen,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static ushort CloseToRtlOpcode(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.Opcode,
            InstructionsEnum.XNOR => CloseToRtlXnor.Opcode,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlHasOpcodeAllocation(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.HasOpcodeAllocation,
            InstructionsEnum.XNOR => CloseToRtlXnor.HasOpcodeAllocation,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlIsExecutable(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.IsExecutable,
            InstructionsEnum.XNOR => CloseToRtlXnor.IsExecutable,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlWritesScalarRegister(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.WritesScalarRegister,
            InstructionsEnum.XNOR => CloseToRtlXnor.WritesScalarRegister,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlHasSideEffects(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.HasSideEffects,
            InstructionsEnum.XNOR => CloseToRtlXnor.HasSideEffects,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlCompilerHelperAllowed(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.CompilerHelperAllowed,
            InstructionsEnum.XNOR => CloseToRtlXnor.CompilerHelperAllowed,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static bool CloseToRtlRequiresVmxProjection(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.RequiresVmxProjection,
            InstructionsEnum.XNOR => CloseToRtlXnor.RequiresVmxProjection,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static ulong CloseToRtlExecute(InstructionsEnum opcode, ulong left, ulong right) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.Execute(left, right),
            InstructionsEnum.XNOR => CloseToRtlXnor.Execute(left, right),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static ulong CloseToRtlEvaluate(InstructionsEnum opcode, ulong left, ulong right) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.EvaluateXLen64(left, right),
            InstructionsEnum.XNOR => CloseToRtlXnor.EvaluateXLen64(left, right),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

    private static IEnumerable<(ulong Left, ulong Right, ulong Result)> GetLocalGoldenVectors(InstructionsEnum opcode) =>
        opcode switch
        {
            InstructionsEnum.ORN => CloseToRtlOrn.GetLocalGoldenVectors()
                .Select(vector => (vector.Left, vector.Right, vector.Result)),
            InstructionsEnum.XNOR => CloseToRtlXnor.GetLocalGoldenVectors()
                .Select(vector => (vector.Left, vector.Right, vector.Result)),
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
