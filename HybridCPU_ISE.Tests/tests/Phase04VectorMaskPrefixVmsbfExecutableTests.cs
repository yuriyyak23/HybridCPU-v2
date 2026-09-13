using System;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class VectorMaskPrefixVmsbfExecutableTests
{
    private const ushort VmsbfImmediate = 1 | (3 << 8);

    [Fact]
    public void Vmsbf_OpcodeValueAndSupportStatus_AreStableAndOnlySelectedPhase04Contour()
    {
        Assert.Equal(53, (int)InstructionsEnum.CZERO_EQZ);
        Assert.Equal(54, (int)InstructionsEnum.CLZ);
        Assert.Null(OpcodeRegistry.GetInfo(55u));
        Assert.Equal(56, (int)InstructionsEnum.SH1ADD);
        Assert.Equal(57, (int)InstructionsEnum.RDCYCLE);
        Assert.Equal(58, (int)InstructionsEnum.CLMUL);
        Assert.Equal(122, (int)InstructionsEnum.VMSBF);
        Assert.Equal(213, (int)InstructionsEnum.VGATHER);
        Assert.Equal(214, (int)InstructionsEnum.VSCATTER);
        Assert.Equal((ushort)InstructionsEnum.VMSBF, IsaOpcodeValues.VMSBF);

        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                "VMSBF",
                out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorMaskPrefixPublication", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Contains("VMSBF", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["VMSBF"]);

        foreach (string mnemonic in new[] { "VMERGE", "VSELECT", "VFIRST", "VANY", "VALL", "VMSIF", "VMSOF" })
        {
            Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(mnemonic, out InstructionSupportStatus closed));
            Assert.Equal(IsaInstructionStatus.Reserved, closed.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, closed.RuntimeEvidence);
            Assert.Equal("VectorMaskSelect", closed.ExtensionName);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic));
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        }
    }

    [Fact]
    public void Vmsbf_ClassifierRegistryLegalityAndMaterializer_PublishTypedPredicateCarrier()
    {
        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.VMSBF));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.VMSBF);
        Assert.NotNull(info);
        Assert.Equal("VMSBF", info!.Value.Mnemonic);
        Assert.True(info.Value.IsVector);
        Assert.True((info.Value.Flags & InstructionFlags.MaskManipulation) != 0);
        Assert.True(OpcodeRegistry.IsMaskManipOp((uint)InstructionsEnum.VMSBF));
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)InstructionsEnum.VMSBF));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)InstructionsEnum.VMSBF));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.VMSBF));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VMSBF);
        Assert.Equal("VectorMaskPrefixPublication", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.TailMaskPolicy);

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VMSBF);
        Assert.NotNull(descriptor);
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VMSBF));

        VectorMaskOpMicroOp microOp = MaterializeVmsbf();
        Assert.Equal((uint)InstructionsEnum.VMSBF, microOp.OpCode);
        Assert.False(microOp.IsMemoryOp);
        Assert.True(microOp.HasSideEffects);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            MaterializeVmsbf(immediate: (ushort)(1 | (2 << 4) | (3 << 8))));
    }

    [Fact]
    public void Vmsbf_DecoderIrAndProjector_UseCanonicalVectorPayloadOnly()
    {
        VLIW_Instruction[] rawSlots = CreateBundle(
            CreateVmsbfInstruction(
                streamLength: 13,
                tailAgnostic: true,
                maskAgnostic: true,
                predicateMask: 0x05));

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8400, bundleSerial: 94);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(0).RequireInstruction();
        Assert.Equal((ushort)InstructionsEnum.VMSBF, ir.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        Assert.Equal(VmsbfImmediate, ir.Imm);
        Assert.Null(ir.DmaStreamComputeDescriptor);
        Assert.Null(ir.AcceleratorCommandDescriptor);

        VectorInstructionPayload payload = Assert.IsType<VectorInstructionPayload>(ir.VectorPayload);
        Assert.Equal(13U, payload.StreamLength);
        Assert.Equal(0UL, payload.PrimaryPointer);
        Assert.Equal(0UL, payload.SecondaryPointer);
        Assert.False(payload.Indexed);
        Assert.False(payload.Is2D);
        Assert.True(payload.TailAgnostic);
        Assert.True(payload.MaskAgnostic);
        Assert.Equal(0x05, payload.PredicateMask);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);
        VectorMaskOpMicroOp microOp = Assert.IsType<VectorMaskOpMicroOp>(carrierBundle[0]);
        Assert.Equal((uint)InstructionsEnum.VMSBF, microOp.OpCode);
        Assert.Equal(13U, microOp.Instruction.StreamLength);
        Assert.True(microOp.Instruction.TailAgnostic);
        Assert.True(microOp.Instruction.MaskAgnostic);
        Assert.Equal(0x05, microOp.Instruction.PredicateMask);
    }

    [Theory]
    [InlineData(0UL, 8U, 0xFFUL)]
    [InlineData(0b0001_0000UL, 8U, 0x0FUL)]
    [InlineData(0b0000_0001UL, 8U, 0UL)]
    [InlineData(0b0010_1000UL, 8U, 0x07UL)]
    [InlineData(0x100UL, 8U, 0xFFUL)]
    [InlineData(ulong.MaxValue, 8U, 0UL)]
    [InlineData(0UL, 64U, 0xFFFF_FFFFUL)]
    [InlineData(0x8000_0000UL, 64U, 0x7FFF_FFFFUL)]
    [InlineData(0x1_0000_0000UL, 64U, 0xFFFF_FFFFUL)]
    public void Vmsbf_Execute_DefinesSetBeforeFirstPredicateSemantics(
        ulong sourceMask,
        uint streamLength,
        ulong expected)
    {
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, sourceMask);
        core.SetPredicateRegister(3, 0xA5A5UL);

        VectorMaskOpMicroOp microOp = MaterializeVmsbf(streamLength: streamLength);

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(expected, core.GetPredicateRegister(3));
        Assert.Equal(sourceMask, core.GetPredicateRegister(1));
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));
    }

    [Fact]
    public void Vmsbf_DirectFactoryPublication_HasNoScalarRegisterOrVectorRfSurface()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x8500);

        VectorMaskOpMicroOp microOp = MaterializeVmsbf();
        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.True(slot.IsVectorOp);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.WriteRegisters);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
    }

    [Fact]
    public void Vmsbf_StreamRetirePublication_ForNonZeroVtAppliesPredicateStateAtRetire()
    {
        const int vtId = 2;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x8600, activeVtId: vtId);
        core.SetPredicateRegister(1, 0b0001_0000UL);
        core.SetPredicateRegister(3, 0xA5UL);

        VLIW_Instruction instruction = CreateVmsbfInstruction(streamLength: 8);

        core.ExecuteDirectStreamCompat(instruction, ownerThreadId: vtId);

        Assert.Equal(0x0FUL, core.GetPredicateRegister(3));
        Assert.Equal(0b0001_0000UL, core.GetPredicateRegister(1));
        Assert.Equal(0UL, core.ReadArch(vtId, 0));
    }

    [Fact]
    public void Vmsbf_IndexedOr2DAndReservedOperandSidebands_FailClosed()
    {
        DecodeProjectionFaultException indexed = Assert.Throws<DecodeProjectionFaultException>(() =>
            MaterializeVmsbf(indexed: true));
        Assert.Contains("unsupported indexed vector-mask addressing", indexed.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", indexed.Message, StringComparison.Ordinal);

        DecodeProjectionFaultException twoDimensional = Assert.Throws<DecodeProjectionFaultException>(() =>
            MaterializeVmsbf(is2D: true));
        Assert.Contains("unsupported 2D vector-mask addressing", twoDimensional.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", twoDimensional.Message, StringComparison.Ordinal);

        InvalidOperationException runtimeReserved = Assert.Throws<InvalidOperationException>(() =>
        {
            var core = new Processor.CPU_Core(0);
            VectorMaskOpMicroOp microOp = new()
            {
                OpCode = (uint)InstructionsEnum.VMSBF,
                Instruction = CreateVmsbfInstruction(immediate: (ushort)(1 | (2 << 4) | (3 << 8)))
            };
            microOp.InitializeMetadata();
            microOp.Execute(ref core);
        });
        Assert.Contains("reserved src2 predicate nibble", runtimeReserved.Message, StringComparison.Ordinal);
    }

    private static VectorMaskOpMicroOp MaterializeVmsbf(
        ushort immediate = VmsbfImmediate,
        uint streamLength = 8,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVmsbfInstruction(
            immediate: immediate,
            streamLength: streamLength,
            indexed: indexed,
            is2D: is2D);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorMaskOpMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VMSBF, context));
    }

    private static VLIW_Instruction[] CreateBundle(VLIW_Instruction instruction)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = instruction;
        return rawSlots;
    }

    private static VLIW_Instruction CreateVmsbfInstruction(
        ushort immediate = VmsbfImmediate,
        uint streamLength = 8,
        bool indexed = false,
        bool is2D = false,
        bool tailAgnostic = false,
        bool maskAgnostic = false,
        byte predicateMask = 0xFF) =>
        new()
        {
            OpCode = (uint)InstructionsEnum.VMSBF,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = predicateMask,
            Immediate = immediate,
            StreamLength = streamLength,
            Indexed = indexed,
            Is2D = is2D,
            TailAgnostic = tailAgnostic,
            MaskAgnostic = maskAgnostic,
            VirtualThreadId = 2
        };

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        return hasEnum || hasRegistryMnemonic;
    }
}
