using System;
using System.Collections.Generic;
using System.Linq;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.MemoryAccelerators;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class L7SdcCompilerEmissionTests
{
    [Fact]
    public void CoarseMatMulIntent_EmitsLane7AccelSubmitWithDescriptorSideband()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var context = CreateContextForDescriptor(descriptor);

        CompilerAcceleratorLoweringDecision decision =
            context.CompileAcceleratorSubmit(
                IrAcceleratorIntent.ForMatMul(descriptor, tokenDestinationRegister: 9),
                CompilerAcceleratorCapabilityModel.ReferenceMatMul);

        Assert.True(decision.EmitsAcceleratorSubmit);
        Assert.Equal(1, context.InstructionCount);

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        Assert.Equal(InstructionsEnum.ACCEL_SUBMIT, (InstructionsEnum)raw.OpCode);
        Assert.Equal(0, raw.Reserved);
        Assert.Equal(0, raw.VirtualThreadId);
        Assert.Equal(0UL, raw.Src2Pointer);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(raw.Word1, out byte rd, out byte rs1, out byte rs2));
        Assert.Equal(9, rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs2);

        VliwBundleAnnotations sourceAnnotations = context.GetBundleAnnotations();
        Assert.True(sourceAnnotations.TryGetInstructionSlotMetadata(0, out InstructionSlotMetadata sourceMetadata));
        Assert.Same(descriptor, sourceMetadata.AcceleratorCommandDescriptor);
        Assert.Null(sourceMetadata.DmaStreamComputeDescriptor);
        Assert.Equal(SlotClass.SystemSingleton, sourceMetadata.SlotMetadata.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, sourceMetadata.SlotMetadata.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, sourceMetadata.SlotMetadata.AdmissionMetadata.Placement.PinnedLaneId);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(InstructionsEnum.ACCEL_SUBMIT, ir.Opcode);
        Assert.Equal(InstructionClass.System, ir.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, ir.SerializationClass);
        Assert.Equal(IrResourceClass.System, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.SystemSingleton, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrIssueSlotMask.Slot7, ir.Annotation.LegalSlots);
        Assert.Same(descriptor, ir.AcceleratorCommandDescriptor);
        Assert.Null(ir.DmaStreamComputeDescriptor);

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? slot));
        Assert.NotNull(slot);
        Assert.Equal(7, slot!.SlotIndex);

        TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(materializedBundle);
        Assert.Equal(1, facts.SystemSingletonCount);
        Assert.Equal(0, facts.BranchControlCount);
        Assert.Equal(0, facts.DmaStreamCount);
        Assert.True(compiledProgram.AdmissibilityAgreement.AllTypedSlotFactsValid);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(7);
        Assert.Equal(InstructionsEnum.ACCEL_SUBMIT, (InstructionsEnum)lowered.OpCode);
        Assert.Equal(0, lowered.Reserved);
        Assert.Equal(0, lowered.VirtualThreadId);
        Assert.Equal(0UL, lowered.Src2Pointer);

        VliwBundleAnnotations loweredAnnotations = Assert.Single(compiledProgram.LoweredBundleAnnotations);
        Assert.True(loweredAnnotations.TryGetInstructionSlotMetadata(7, out InstructionSlotMetadata loweredMetadata));
        Assert.Same(descriptor, loweredMetadata.AcceleratorCommandDescriptor);

        MicroOp projected = DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            loweredAnnotations,
            slotIndex: 7);
        AcceleratorSubmitMicroOp submit = Assert.IsType<AcceleratorSubmitMicroOp>(projected);
        Assert.Same(descriptor, submit.CommandDescriptor);
        Assert.Equal(SlotClass.SystemSingleton, submit.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, submit.Placement.PinningKind);
        Assert.Equal(7, submit.Placement.PinnedLaneId);
        Assert.False(submit.WritesRegister);
        Assert.Empty(submit.WriteRegisters);

        _ = new Processor(ProcessorMode.Emulation);
        _ = compiledProgram.EmitVliwBundleImage(baseAddress: 0);
        Assert.True(Processor.MainMemory.TryReadVliwBundleAnnotations(
            0,
            out VliwBundleAnnotations? emittedAnnotations));
        Assert.NotNull(emittedAnnotations);
        Assert.True(emittedAnnotations!.TryGetInstructionSlotMetadata(
            7,
            out InstructionSlotMetadata emittedMetadata));
        Assert.Same(descriptor, emittedMetadata.AcceleratorCommandDescriptor);
    }

    [Fact]
    public void ExplicitIntentOnly_DirectCompilerEmissionOfAccelOpcodeRejects()
    {
        var context = new HybridCpuThreadCompilerContext(0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => context.CompileInstruction(
                opCode: (uint)InstructionsEnum.ACCEL_SUBMIT,
                dataType: 0,
                predicate: 0,
                immediate: 0,
                destSrc1: VLIW_Instruction.PackArchRegs(
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                src2: 0,
                streamLength: 0,
                stride: 0,
                stealabilityPolicy: StealabilityPolicy.NotStealable));

        Assert.Contains("explicit accelerator intent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void UnsupportedCapability_ChoosesCpuLoweringBeforeAccelSubmitEmission()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var context = CreateContextForDescriptor(descriptor);

        CompilerAcceleratorLoweringDecision decision =
            context.CompileAcceleratorSubmit(
                IrAcceleratorIntent.ForMatMul(descriptor),
                CompilerAcceleratorCapabilityModel.Disabled);

        Assert.True(decision.UsesNonAcceleratorLowering);
        Assert.False(decision.EmitsAcceleratorSubmit);
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void UnsupportedProviderShape_ChoosesCpuLoweringBeforeAccelSubmitEmission()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        ulong oversizedElementCount = MatMulDescriptorValidator.MaxOutputElements + 1;
        AcceleratorCommandDescriptor oversizedDescriptor = descriptor with
        {
            ElementCount = oversizedElementCount,
            Header = descriptor.Header with { ElementCount = oversizedElementCount }
        };
        var context = CreateContextForDescriptor(oversizedDescriptor);

        CompilerAcceleratorLoweringDecision decision =
            context.CompileAcceleratorSubmit(
                IrAcceleratorIntent.ForMatMul(oversizedDescriptor),
                CompilerAcceleratorCapabilityModel.ReferenceMatMul);

        Assert.True(decision.UsesNonAcceleratorLowering);
        Assert.False(decision.EmitsAcceleratorSubmit);
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void MalformedAcceleratorIntent_RejectsBeforeEmission()
    {
        var context = new HybridCpuThreadCompilerContext(0);
        var intent = new IrAcceleratorIntent
        {
            Operation = AcceleratorOperationKind.MatMul,
            DescriptorSideband = null!
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => context.CompileAcceleratorSubmit(
                intent,
                CompilerAcceleratorCapabilityModel.ReferenceMatMul));

        Assert.Contains("descriptor sideband", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void InvalidTokenDestinationRegister_RejectsBeforeEmission()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var context = CreateContextForDescriptor(descriptor);
        IrAcceleratorIntent intent =
            IrAcceleratorIntent.ForMatMul(descriptor, tokenDestinationRegister: 32);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => context.CompileAcceleratorSubmit(
                intent,
                CompilerAcceleratorCapabilityModel.ReferenceMatMul));
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void UnknownAcceleratorLoweringMode_RejectsBeforeEmission()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var context = CreateContextForDescriptor(descriptor);
        IrAcceleratorIntent intent =
            IrAcceleratorIntent.ForMatMul(descriptor) with
            {
                RequestedMode = (AcceleratorLoweringMode)0x7F
            };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => context.CompileAcceleratorSubmit(
                intent,
                CompilerAcceleratorCapabilityModel.ReferenceMatMul));
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void RegularStreamIntent_RemainsLane6DmaStreamCompute()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var context = new HybridCpuThreadCompilerContext(0);

        context.CompileDmaStreamCompute(descriptor);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(InstructionsEnum.DmaStreamCompute, ir.Opcode);
        Assert.Equal(IrResourceClass.DmaStream, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.DmaStreamClass, ir.Annotation.RequiredSlotClass);
        Assert.Same(descriptor, ir.DmaStreamComputeDescriptor);
        Assert.Null(ir.AcceleratorCommandDescriptor);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(6);
        Assert.Equal(InstructionsEnum.DmaStreamCompute, (InstructionsEnum)lowered.OpCode);
        Assert.True(compiledProgram.LoweredBundleAnnotations[0]
            .TryGetInstructionSlotMetadata(6, out InstructionSlotMetadata metadata));
        Assert.Same(descriptor, metadata.DmaStreamComputeDescriptor);
        Assert.Null(metadata.AcceleratorCommandDescriptor);
    }

    [Fact]
    public void DmaStreamSidebandOnNonDmaOpcode_RejectsAtIrBuild()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var raw = new[]
        {
            new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.Addition,
                DataTypeValue = DataTypeEnum.INT32,
                Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3)
            }
        };
        var annotations = new VliwBundleAnnotations(new[]
        {
            InstructionSlotMetadata.Default with
            {
                DmaStreamComputeDescriptor = descriptor
            }
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => HybridCpuCanonicalCompiler.CompileProgram(
                virtualThreadId: 0,
                instructions: raw,
                bundleAnnotations: annotations));

        Assert.Contains("DmaStreamCompute descriptor sideband", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescriptorSidebandOnEmptySlot_RejectsAtDecoder()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var slotMetadata = Enumerable
            .Repeat(InstructionSlotMetadata.Default, BundleMetadata.BundleSlotCount)
            .ToArray();
        slotMetadata[7] = InstructionSlotMetadata.Default.WithAcceleratorDescriptor(descriptor);
        var annotations = new VliwBundleAnnotations(slotMetadata);
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                annotations,
                bundleAddress: 0xD000,
                bundleSerial: 4));

        Assert.Contains("empty/NOP", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProjectorRejectsDmaStreamSidebandOnNonDmaInstructionIr()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Addition,
            DataTypeValue = DataTypeEnum.INT32,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3)
        };
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.Addition,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 1,
            Rs1 = 2,
            Rs2 = 3,
            Imm = 0,
            DmaStreamComputeDescriptor = descriptor,
            DmaStreamComputeDescriptorReference = descriptor.DescriptorReference
        };
        var decoded = new DecodedInstructionBundle(
            bundleAddress: 0xD100,
            bundleSerial: 5,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(0, instruction)
            });

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);

        TrapMicroOp trap = Assert.IsType<TrapMicroOp>(carriers[0]);
        Assert.Contains("non-DmaStreamCompute", trap.TrapReason, StringComparison.OrdinalIgnoreCase);
    }

    internal static HybridCpuThreadCompilerContext CreateContextForDescriptor(
        AcceleratorCommandDescriptor descriptor) =>
        new(checked((byte)descriptor.OwnerBinding.OwnerVirtualThreadId))
        {
            DomainTag = descriptor.OwnerBinding.DomainTag
        };

    internal static MicroOp DecodeAndProjectSingleCarrier(
        VLIW_Bundle bundle,
        VliwBundleAnnotations annotations,
        int slotIndex)
    {
        VLIW_Instruction[] rawSlots = ToRawSlots(bundle);
        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            rawSlots,
            annotations,
            bundleAddress: 0xC000,
            bundleSerial: 12);
        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);
        return carriers[slotIndex] ?? throw new InvalidOperationException("Expected occupied projected carrier.");
    }

    internal static VLIW_Instruction[] ToRawSlots(VLIW_Bundle bundle)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = bundle.GetInstruction(slotIndex);
        }

        return rawSlots;
    }
}

public sealed class L7SdcCompilerNoRuntimeFallbackTests
{
    [Fact]
    public void RuntimeRejectAfterCompilerEmission_RemainsSystemDeviceRejectWithoutFallbackCarrier()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        HybridCpuThreadCompilerContext context =
            L7SdcCompilerEmissionTests.CreateContextForDescriptor(descriptor);
        context.CompileAcceleratorSubmit(
            IrAcceleratorIntent.ForMatMul(descriptor),
            CompilerAcceleratorCapabilityModel.ReferenceMatMul);
        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            slotIndex: 7);

        AcceleratorSubmitMicroOp submit = Assert.IsType<AcceleratorSubmitMicroOp>(carrier);
        Assert.IsNotType<DmaStreamComputeMicroOp>(submit);
        Assert.IsNotType<GenericMicroOp>(submit);
        Assert.IsNotType<TrapMicroOp>(submit);

        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            if (Processor.MainMemory is null)
            {
                Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x10000);
            }

            var core = new Processor.CPU_Core(0);
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => submit.Execute(ref core));

            Assert.Contains("direct execution is unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fallback routing", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void RuntimeFallbackPromiseInIntent_RejectsBeforeEmission()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        HybridCpuThreadCompilerContext context =
            L7SdcCompilerEmissionTests.CreateContextForDescriptor(descriptor);
        IrAcceleratorIntent intent =
            IrAcceleratorIntent.ForMatMul(descriptor) with
            {
                AllowRuntimeFallbackAfterSubmit = true
            };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => context.CompileAcceleratorSubmit(
                intent,
                CompilerAcceleratorCapabilityModel.ReferenceMatMul));

        Assert.Contains("runtime fallback", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.InstructionCount);
    }
}

public sealed class L7SdcCompilerLane7PressureTests
{
    [Fact]
    public void RepeatedSubmitIntent_DoesNotPackDenseLane7StormIntoOneBundle()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        HybridCpuThreadCompilerContext context =
            L7SdcCompilerEmissionTests.CreateContextForDescriptor(descriptor);

        context.CompileAcceleratorSubmit(
            IrAcceleratorIntent.ForMatMul(descriptor),
            CompilerAcceleratorCapabilityModel.ReferenceMatMul);
        context.CompileAcceleratorSubmit(
            IrAcceleratorIntent.ForMatMul(descriptor),
            CompilerAcceleratorCapabilityModel.ReferenceMatMul);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IReadOnlyList<IrMaterializedBundle> bundles =
            compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles).ToArray();

        Assert.Equal(2, bundles.Count);
        foreach (IrMaterializedBundle bundle in bundles)
        {
            TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(bundle);
            Assert.Equal(1, facts.SystemSingletonCount);
            Assert.Equal(0, facts.BranchControlCount);
            Assert.True(HybridCpuTypedSlotFactsEmitter.ValidateEmittedFacts(facts));
        }
    }

    [Fact]
    public void BranchAndSubmit_DoNotShareAliasedLane7BundleWindow()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        HybridCpuThreadCompilerContext context =
            L7SdcCompilerEmissionTests.CreateContextForDescriptor(descriptor);

        context.CompileAcceleratorSubmit(
            IrAcceleratorIntent.ForMatMul(descriptor),
            CompilerAcceleratorCapabilityModel.ReferenceMatMul);
        context.CompileInstruction(
            opCode: (uint)InstructionsEnum.JAL,
            dataType: 0,
            predicate: 0,
            immediate: 4,
            destSrc1: VLIW_Instruction.PackArchRegs(
                2,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            src2: 0,
            streamLength: 0,
            stride: 0,
            stealabilityPolicy: StealabilityPolicy.NotStealable);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IReadOnlyList<IrMaterializedBundle> bundles =
            compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles).ToArray();

        Assert.Equal(2, bundles.Count);
        Assert.DoesNotContain(
            bundles,
            bundle =>
            {
                TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(bundle);
                return facts.SystemSingletonCount > 0 && facts.BranchControlCount > 0;
            });
        Assert.Contains(bundles, bundle => HybridCpuBundleLowerer.EmitFactsForBundle(bundle).SystemSingletonCount == 1);
        Assert.Contains(bundles, bundle => HybridCpuBundleLowerer.EmitFactsForBundle(bundle).BranchControlCount == 1);
    }
}
