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
using CloseToRtlCpop = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount.CpopInstruction;
using CloseToRtlPopcnt = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount.PopcntInstruction;
using CloseToRtlCsel = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ConditionalSelect.CselInstruction;
using CloseToRtlSeqz = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare.SeqzInstruction;
using CloseToRtlSnez = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare.SnezInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxPhase01BitCountExecutableTests
{
    [Fact]
    public void Cpop_OpcodeStatusAndAliasDecision_AreRuntimeClosedWhilePopcntStaysNoEmission()
    {
        Assert.Equal(334, (int)InstructionsEnum.CPOP);
        Assert.Equal((ushort)InstructionsEnum.CPOP, IsaOpcodeValues.CPOP);
        Assert.Equal(InternalOpKind.Cpop, InternalOpBuilder.MapToKind(IsaOpcodeValues.CPOP));

        InstructionSupportStatus cpop = InstructionSupportStatusCatalog.GetStatus("CPOP");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, cpop.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, cpop.RuntimeEvidence);
        Assert.True(cpop.IsExecutableClaim);
        Assert.True(cpop.HasNumericOpcode);
        Assert.True(cpop.HasRuntimeOpcodeMetadata);
        Assert.True(cpop.HasCanonicalDecoderAcceptance);
        Assert.True(cpop.HasRegistryFactory);
        Assert.True(cpop.HasExecutionSemantics);

        Assert.Contains("CPOP", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("CPOP", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain("CPOP", IsaV4Surface.OptionalDisabledOpcodes);

        Assert.Equal("CPOP", CloseToRtlCpop.Mnemonic);
        Assert.Equal("ExecutableScalarAlu", CloseToRtlCpop.EvidenceBoundary);
        Assert.Equal((ushort)InstructionsEnum.CPOP, CloseToRtlCpop.Opcode);
        Assert.True(CloseToRtlCpop.CanonicalMnemonicDecisionClosed);
        Assert.True(CloseToRtlCpop.PopcntAliasNoEmissionPolicyClosed);
        Assert.True(CloseToRtlCpop.HasOpcodeAllocation);
        Assert.True(CloseToRtlCpop.IsExecutable);
        Assert.True(CloseToRtlCpop.WritesScalarRegister);
        Assert.False(CloseToRtlCpop.HasSideEffects);
        Assert.False(CloseToRtlCpop.CompilerHelperAllowed);

        InstructionSupportStatus popcnt = InstructionSupportStatusCatalog.GetStatus("POPCNT");
        Assert.Equal(IsaInstructionStatus.Reserved, popcnt.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, popcnt.RuntimeEvidence);
        Assert.False(popcnt.IsExecutableClaim);
        Assert.False(popcnt.HasNumericOpcode);
        Assert.False(HasEnumOrRegistryMnemonic("POPCNT"));
        Assert.Equal("FacadeAliasNoEmissionClosed", CloseToRtlPopcnt.EvidenceBoundary);
        Assert.True(CloseToRtlPopcnt.CanonicalMnemonicDecisionClosed);
        Assert.True(CloseToRtlPopcnt.SelectedAsNoEmissionAlias);
        Assert.False(CloseToRtlPopcnt.SelectedAsRuntimeMnemonic);
        Assert.False(CloseToRtlPopcnt.HasOpcodeAllocation);
        Assert.False(CloseToRtlPopcnt.IsExecutable);
    }

    [Fact]
    public void FacadeAndCarrierDecisions_StayClosedWithoutHiddenRuntimeAuthority()
    {
        Assert.Equal("FacadeOnlyNoEmissionClosed", CloseToRtlSeqz.EvidenceBoundary);
        Assert.True(CloseToRtlSeqz.FacadeDecisionClosed);
        Assert.True(CloseToRtlSeqz.SelectedFacadeOnly);
        Assert.False(CloseToRtlSeqz.SelectedHardwareOpcode);
        Assert.False(CloseToRtlSeqz.HiddenLoweringAllowed);
        Assert.False(CloseToRtlSeqz.HasOpcodeAllocation);
        Assert.False(CloseToRtlSeqz.IsExecutable);

        Assert.Equal("FacadeOnlyNoEmissionClosed", CloseToRtlSnez.EvidenceBoundary);
        Assert.True(CloseToRtlSnez.FacadeDecisionClosed);
        Assert.True(CloseToRtlSnez.SelectedFacadeOnly);
        Assert.False(CloseToRtlSnez.SelectedHardwareOpcode);
        Assert.False(CloseToRtlSnez.HiddenLoweringAllowed);
        Assert.False(CloseToRtlSnez.HasOpcodeAllocation);
        Assert.False(CloseToRtlSnez.IsExecutable);

        foreach (string mnemonic in new[] { "SEQZ", "SNEZ" })
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.IsExecutableClaim);
            Assert.False(status.HasNumericOpcode);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic));
        }

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
    public void Cpop_ClassifierRegistryAndMaterializer_PublishUnaryScalarAluMicroOp()
    {
        const byte rd = 7;
        const byte rs1 = 5;

        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.CPOP));

        OpcodeInfo info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.CPOP)!.Value;
        Assert.Equal("CPOP", info.Mnemonic);
        Assert.Equal(OpcodeCategory.BitManip, info.Category);
        Assert.Equal(1, info.OperandCount);
        Assert.Equal(InstructionFlags.None, info.Flags);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.SerializationClass);
        Assert.False(info.IsVector);
        Assert.False(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.CPOP));

        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.CPOP)!;
        Assert.Equal(1, descriptor.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.True(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.CPOP));

        MicroOp materialized = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.CPOP,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.CPOP,
                Reg1ID = rd,
                Reg2ID = rs1,
                Reg3ID = 0,
                HasImmediate = true,
                Immediate = 0,
            });

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(materialized);
        Assert.Equal((uint)InstructionsEnum.CPOP, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { (int)rs1 }, scalar.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, scalar.WriteRegisters);

        var internalBuilder = new InternalOpBuilder();
        InternalOp internalOp = internalBuilder.Build(CreateInstructionIr(rd, rs1));
        Assert.Equal(InternalOpKind.Cpop, internalOp.Kind);
        Assert.Equal(InternalOpCategory.Computation, internalOp.Category);
    }

    [Fact]
    public void Cpop_DecoderIrAndProjector_RequireCanonicalUnaryRegisterPayload()
    {
        const byte rd = 8;
        const byte rs1 = 9;

        VLIW_Instruction instruction = CreateScalarInstruction(InstructionsEnum.CPOP, rd, rs1);
        var decoder = new VliwDecoderV4();
        VLIW_Instruction[] rawSlots = CreateBundle(instruction);
        DecodedInstructionBundle bundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0xCA00, bundleSerial: 500);

        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.CPOP, ir.CanonicalOpcode.Value);
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
        Assert.Equal((uint)InstructionsEnum.CPOP, scalar.OpCode);
        Assert.Equal(rd, scalar.DestRegID);
        Assert.Equal(rs1, scalar.Src1RegID);
        Assert.Equal(VLIW_Instruction.NoReg, scalar.Src2RegID);

        VLIW_Instruction registerAlias = CreateScalarInstruction(InstructionsEnum.CPOP, rd, rs1, rs2: 3);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(registerAlias), 0xCA20, 501));

        VLIW_Instruction immediateAlias = CreateScalarInstruction(InstructionsEnum.CPOP, rd, rs1, immediate: 1);
        Assert.Throws<InvalidOperationException>(() =>
            decoder.DecodeInstructionBundle(CreateBundle(immediateAlias), 0xCA40, 502));
    }

    [Theory]
    [InlineData(0UL, 0UL)]
    [InlineData(1UL, 1UL)]
    [InlineData(0x8000_0000_0000_0000UL, 1UL)]
    [InlineData(0x0123_4567_89AB_CDEFUL, 32UL)]
    [InlineData(ulong.MaxValue, 64UL)]
    public void Cpop_ScalarAluCloseToRtlAndGoldenVectors_DefineXlen64PopulationCount(
        ulong source,
        ulong expected)
    {
        ulong actual = ScalarAluOps.Compute((uint)InstructionsEnum.CPOP, source, op2: 0, immediate: 0);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, CloseToRtlCpop.Execute(source));
        Assert.Equal(expected, CloseToRtlCpop.EvaluateXLen64(source));

        foreach (var vector in CloseToRtlCpop.GetLocalGoldenVectors())
        {
            Assert.Equal(
                vector.Result,
                ScalarAluOps.Compute((uint)InstructionsEnum.CPOP, vector.Source, op2: 0, immediate: 0));
        }
    }

    [Fact]
    public void Cpop_MainlinePipelineRetireAndRollback_PreserveScalarTruth()
    {
        const int vtId = 2;
        const ulong pc = 0xCB00UL;
        const ushort sourceRegister = 5;
        const ushort destinationRegister = 7;
        const ulong sourceValue = 0xFFFF_FFFF_0000_0000UL;
        const ulong expectedResult = 32UL;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, pc, out long serializingEpochCountBefore);
        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.CPOP,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister,
                virtualThreadId: (byte)vtId);
        ScalarALUMicroOp microOp = DecodeAndMaterializeScalar(instruction, vtId);
        HybridCPU_ISE.Core.ReplayToken rollbackToken = microOp.CreateRollbackToken(vtId);
        rollbackToken.CaptureRegisterState(ref core, [(int)destinationRegister]);

        core.TestRunDecodeStageWithFetchedBundle(CreateBundle(instruction), pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(0UL, core.ReadArch(vtId, 0));
        AssertReplayPhasePreserved(core, scheduler, serializingEpochCountBefore);

        rollbackToken.Rollback(ref core);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void Cpop_CompilerHelperOpensWithoutPopcntAliasOrVmxAuthority()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        string compilerEmissionSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        string vmxSource = ReadAllSource(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_ISE", "Core", "VMX"));

        Assert.Contains("InstructionsEnum.CPOP", compilerSource, StringComparison.Ordinal);
        Assert.Contains("CountSetBits", compilerSource, StringComparison.Ordinal);

        foreach (string forbidden in new[] { "Cpop", "CPop", "POPCNT", "Popcnt", "PopulationCount", "CountPopulation", "SEQZ", "SNEZ", "CSEL" })
        {
            Assert.DoesNotContain(forbidden, compilerEmissionSource, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("CPOP", vmxSource, StringComparison.Ordinal);
        Assert.DoesNotContain("POPCNT", vmxSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SEQZ", vmxSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SNEZ", vmxSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CSEL", vmxSource, StringComparison.Ordinal);
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0xCC00, bundleSerial: 503);
        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(carrierBundle[0]);
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
    }

    private static InstructionIR CreateInstructionIr(byte rd, byte rs1)
    {
        return new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.CPOP,
            Class = InstructionClassifier.GetClass(InstructionsEnum.CPOP),
            SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.CPOP),
            Rd = rd,
            Rs1 = rs1,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0
        };
    }

    private static VLIW_Instruction[] CreateBundle(params VLIW_Instruction[] slots)
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
