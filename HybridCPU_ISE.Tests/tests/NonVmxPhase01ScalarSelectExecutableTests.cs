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
using CloseToRtlCsel = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ConditionalSelect.CselInstruction;
using CloseToRtlCzeroNez = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ZeroingSelect.CzeroNezInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxPhase01ScalarSelectExecutableTests
{
    [Fact]
    public void CzeroNez_OpcodeStatusAndCloseToRtlObject_AreRuntimeClosedWhileCselStaysGated()
    {
        Assert.Equal(333, (int)InstructionsEnum.CZERO_NEZ);
        Assert.Equal((ushort)InstructionsEnum.CZERO_NEZ, IsaOpcodeValues.CZERO_NEZ);
        Assert.Equal(InternalOpKind.CzeroNez, InternalOpBuilder.MapToKind(IsaOpcodeValues.CZERO_NEZ));

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "CZERO.NEZ",
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

        Assert.Contains("CZERO.NEZ", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("CZERO.NEZ", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain("CZERO.NEZ", IsaV4Surface.OptionalDisabledOpcodes);

        Assert.Equal("CZERO.NEZ", CloseToRtlCzeroNez.Mnemonic);
        Assert.Equal("rd, rs1, rs2", CloseToRtlCzeroNez.OperandShape);
        Assert.Equal("ExecutableScalarAlu", CloseToRtlCzeroNez.EvidenceBoundary);
        Assert.Equal("ConditionNotEqualZeroProducesZero", CloseToRtlCzeroNez.Polarity);
        Assert.Equal(64, CloseToRtlCzeroNez.XLen);
        Assert.Equal((ushort)InstructionsEnum.CZERO_NEZ, CloseToRtlCzeroNez.Opcode);
        Assert.True(CloseToRtlCzeroNez.PolarityProofClosed);
        Assert.True(CloseToRtlCzeroNez.SeparateFromClosedCzeroEqz);
        Assert.True(CloseToRtlCzeroNez.HasOpcodeAllocation);
        Assert.True(CloseToRtlCzeroNez.IsExecutable);
        Assert.True(CloseToRtlCzeroNez.WritesScalarRegister);
        Assert.False(CloseToRtlCzeroNez.HasSideEffects);
        Assert.False(CloseToRtlCzeroNez.CompilerHelperAllowed);
        Assert.False(CloseToRtlCzeroNez.RequiresVmxProjection);

        InstructionSupportStatus cselStatus = InstructionSupportStatusCatalog.GetStatus("CSEL");
        Assert.Equal(IsaInstructionStatus.Reserved, cselStatus.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, cselStatus.RuntimeEvidence);
        Assert.False(cselStatus.IsExecutableClaim);
        Assert.Equal("ScalarSelectAbiDeferredNoEmission", CloseToRtlCsel.EvidenceBoundary);
        Assert.Equal("Phase01ECarrierGateClosedNoApprovedCarrier", CloseToRtlCsel.CarrierGateDecision);
        Assert.True(CloseToRtlCsel.RequiresFourRegisterCarrierAbi);
        Assert.True(CloseToRtlCsel.FourSourceCarrierDecisionClosed);
        Assert.True(CloseToRtlCsel.ExternalCarrierGateClosed);
        Assert.False(CloseToRtlCsel.ApprovedFourSourceCarrier);
        Assert.False(CloseToRtlCsel.ExternalCarrierApprovedInPhase01);
        Assert.False(CloseToRtlCsel.CurrentPackedScalarIrSupportsCarrier);
        Assert.True(CloseToRtlCsel.RequiresExternalCarrierAbi);
        Assert.False(CloseToRtlCsel.HasOpcodeAllocation);
        Assert.False(CloseToRtlCsel.IsExecutable);
    }

    [Fact]
    public void CzeroNez_ClassifierRegistryAndMaterializer_PublishScalarAluMicroOp()
    {
        const byte rd = 7;
        const byte rs1 = 5;
        const byte rs2 = 6;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.CZERO_NEZ));

        OpcodeInfo info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.CZERO_NEZ)!.Value;
        Assert.Equal("CZERO.NEZ", info.Mnemonic);
        Assert.Equal(OpcodeCategory.Scalar, info.Category);
        Assert.Equal(2, info.OperandCount);
        Assert.Equal(InstructionFlags.TwoOperand, info.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.False(info.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.CZERO_NEZ));

        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.CZERO_NEZ)!;
        Assert.Equal(1, descriptor.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.CZERO_NEZ));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.CZERO_NEZ,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.CZERO_NEZ,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = rs2,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.CZERO_NEZ, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.CZERO_NEZ,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.CZERO_NEZ,
                    Reg1ID = rd,
                    Reg2ID = rs1,
                    Reg3ID = rs2,
                    HasImmediate = true,
                    Immediate = 1,
                }));

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(rd, rs1, rs2));
        Assert.Equal(InternalOpKind.CzeroNez, internalOp.Kind);
        Assert.Equal(InternalOpDataType.DWord, internalOp.DataType);
        Assert.Equal(InternalOpFlags.None, internalOp.Flags);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Fact]
    public void CzeroNez_DecoderIrAndProjector_RequireCanonicalBinaryRegisterPayload()
    {
        const byte rd = 8;
        const byte rs1 = 9;
        const byte rs2 = 10;

        VLIW_Instruction instruction = CreateScalarInstruction(
            InstructionsEnum.CZERO_NEZ,
            rd,
            rs1,
            rs2);
        Assert.Equal(0, instruction.Immediate);

        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0xC300, bundleSerial: 430);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.CZERO_NEZ, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(rd, ir.Rd);
        Assert.Equal(rs1, ir.Rs1);
        Assert.Equal(rs2, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.Null(ir.VectorPayload);
        Assert.Null(ir.DmaStreamComputeDescriptor);
        Assert.Null(ir.AcceleratorCommandDescriptor);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)InstructionsEnum.CZERO_NEZ, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(rs2, scalar.Src2RegID);
        Assert.Equal(new[] { (int)rs1, (int)rs2 }, scalar.ReadRegisters);

        VLIW_Instruction immediateAlias = CreateScalarInstruction(
            InstructionsEnum.CZERO_NEZ,
            rd,
            rs1,
            rs2,
            immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0xC340, 431));
    }

    [Theory]
    [InlineData(0UL, 0UL, 0UL)]
    [InlineData(1UL, 0UL, 1UL)]
    [InlineData(0x0123_4567_89AB_CDEFUL, 0UL, 0x0123_4567_89AB_CDEFUL)]
    [InlineData(0x0123_4567_89AB_CDEFUL, 1UL, 0UL)]
    [InlineData(ulong.MaxValue, 0UL, ulong.MaxValue)]
    [InlineData(ulong.MaxValue, 0x8000_0000_0000_0000UL, 0UL)]
    public void CzeroNez_ScalarAluCloseToRtlAndGoldenVectors_DefineSeparateNonzeroPolarity(
        ulong source,
        ulong condition,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute(
            (uint)InstructionsEnum.CZERO_NEZ,
            source,
            condition,
            immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, CloseToRtlCzeroNez.Execute(source, condition));
        Assert.Equal(expected, CloseToRtlCzeroNez.EvaluateXLen64(source, condition));

        foreach (var vector in CloseToRtlCzeroNez.GetLocalGoldenVectors())
        {
            Assert.Equal(
                vector.Result,
                ScalarAluOps.Compute(
                    (uint)InstructionsEnum.CZERO_NEZ,
                    vector.Value,
                    vector.Condition,
                    immediate: 0));
        }
    }

    [Theory]
    [InlineData(2, 0UL, 0x1234_5678_9ABC_DEF0UL)]
    [InlineData(3, 0xCAFE_BABE_F00D_1234UL, 0UL)]
    public void CzeroNez_MainlinePipeline_RetiresForNonZeroVtWithoutEarlyPublication(
        int vtId,
        ulong condition,
        ulong expectedResult)
    {
        const ulong pc = 0xC400UL;
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
                InstructionsEnum.CZERO_NEZ,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                rs2: (byte)conditionRegister));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);

        var decodeStatus = core.TestReadDecodeStageStatus();
        Assert.True(decodeStatus.Valid);
        Assert.Equal((uint)InstructionsEnum.CZERO_NEZ, decodeStatus.OpCode);
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
    public void CzeroNez_WriteToX0_IsDiscardedAtRetire()
    {
        const int vtId = 1;
        const ulong pc = 0xC500UL;
        const ushort sourceRegister = 8;
        const ushort conditionRegister = 9;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, 0xFFFF_FFFF_FFFF_FFFFUL);
        core.WriteCommittedArch(vtId, conditionRegister, 0UL);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(
                InstructionsEnum.CZERO_NEZ,
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
    public void CzeroNez_DispatcherCapture_PublishesRetireRecordWithoutEagerStateMutation()
    {
        const byte vtId = 2;
        const byte rd = 11;
        const byte rs1 = 12;
        const byte rs2 = 13;
        const ulong sourceValue = 0xFEED_FACE_CAFE_BEEFUL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, rs1, sourceValue);
        core.WriteCommittedArch(vtId, rs2, 0UL);

        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var dispatcher = new ExecutionDispatcherV4();
        InstructionIR instruction = CreateInstructionIr(rd, rs1, rs2);

        RetireWindowCaptureSnapshot snapshot =
            RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial: 432,
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
            bundleSerial: 432,
            vtId);

        Assert.Equal(sourceValue, core.ReadArch(vtId, rd));
    }

    [Fact]
    public void CzeroNez_ReplayRollbackAfterWriteback_RestoresArchitecturalTruth()
    {
        const int vtId = 2;
        const ulong pc = 0xC600UL;
        const ushort destinationRegister = 14;
        const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
        const ulong sourceValue = 0xDEAD_BEEF_DEAD_BEEFUL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, 5, sourceValue);
        core.WriteCommittedArch(vtId, 6, 0UL);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.CZERO_NEZ,
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
    public void ScalarSelect_CompilerAndVmxGates_RemainGenericNoEmission()
    {
        string compilerSource = ReadAllSource(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler"));
        string vmxSource = ReadAllSource(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_ISE", "Core", "VMX"));

        Assert.DoesNotContain("CZERO_NEZ", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CZERO.NEZ", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CzeroNez", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Csel", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConditionalSelect", compilerSource, StringComparison.Ordinal);

        Assert.DoesNotContain("CZERO_NEZ", vmxSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CZERO.NEZ", vmxSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CSEL", vmxSource, StringComparison.Ordinal);

        OpcodeInfo info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.CZERO_NEZ)!.Value;
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.NotEqual(InstructionClass.Vmx, info.InstructionClass);
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0xC700, bundleSerial: 433);
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
        byte rd,
        byte rs1,
        byte rs2)
    {
        return new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.CZERO_NEZ,
            Class = InstructionClassifier.GetClass(InstructionsEnum.CZERO_NEZ),
            SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.CZERO_NEZ),
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
