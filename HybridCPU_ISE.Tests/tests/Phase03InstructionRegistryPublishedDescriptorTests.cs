using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03InstructionRegistryPublishedDescriptorTests
{
    [Fact]
    public void PublishedOpcodeRegistrySurface_RemainsClassifierAuthoritativeForAllPublishedOpcodes()
    {
        foreach (OpcodeInfo info in OpcodeRegistry.Opcodes)
        {
            var opcode = (InstructionsEnum)info.OpCode;

            Assert.True(
                OpcodeRegistry.TryGetPublishedSemantics(
                    opcode,
                    out InstructionClass publishedClass,
                    out SerializationClass publishedSerialization));
            Assert.Equal(info.InstructionClass, publishedClass);
            Assert.Equal(info.SerializationClass, publishedSerialization);
            Assert.Equal(info.InstructionClass, InstructionClassifier.GetClass(opcode));
            Assert.Equal(info.SerializationClass, InstructionClassifier.GetSerializationClass(opcode));
        }
    }

    [Fact]
    public void PublishedOpcodeRegistrySurface_RuntimeDescriptorGapIsClosedForAllPublishedOpcodes()
    {
        foreach (OpcodeInfo info in OpcodeRegistry.Opcodes)
        {
            MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor(info.OpCode);
            Assert.NotNull(descriptor);

            var opcode = (InstructionsEnum)info.OpCode;
            bool latencyMatches = descriptor!.Latency == info.ExecutionLatency;
            bool isKnownLegacyDescriptorOverride = IsKnownLegacyDescriptorLatencyOverride(opcode);

            Assert.True(
                latencyMatches || isKnownLegacyDescriptorOverride,
                $"Runtime descriptor latency mismatch for opcode {opcode} (0x{info.OpCode:X}): expected registry latency {info.ExecutionLatency}, actual descriptor latency {descriptor.Latency}.");
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.JumpIfEqual, 0, false)]
    [InlineData(InstructionsEnum.VGATHER, 3, false)]
    [InlineData(InstructionsEnum.VSCATTER, 3, false)]
    public void PublishedDescriptorOnlyContours_PreserveDescriptorSurfaceWithoutRevivingCarrierRegistration(
        InstructionsEnum opcode,
        int expectedMemFootprintClass,
        bool expectedRegistration)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(expectedMemFootprintClass, descriptor.MemFootprintClass);
        Assert.Equal(expectedRegistration, InstructionRegistry.IsRegistered((uint)opcode));
    }

    [Fact]
    public void PublishedDescriptors_DoNotExposeLegacyCanBeStolenProperty()
    {
        PropertyInfo? property = typeof(MicroOpDescriptor).GetProperty("CanBeStolen");
        Assert.Null(property);
    }

    [Theory]
    [InlineData(InstructionsEnum.FENCE)]
    [InlineData(InstructionsEnum.ECALL)]
    [InlineData(InstructionsEnum.YIELD)]
    public void PublishedSystemDescriptors_UseOpcodeRegistryLatencyAndPreservePublishedDescriptorSurface(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
    }

    [Theory]
    [InlineData(InstructionsEnum.STREAM_SETUP, InstructionClass.System)]
    [InlineData(InstructionsEnum.STREAM_START, InstructionClass.System)]
    [InlineData(InstructionsEnum.STREAM_WAIT, InstructionClass.SmtVt)]
    public void PublishedStreamDescriptors_UseOpcodeRegistryLatencyAndPreserveRuntimeFootprint(
        InstructionsEnum opcode,
        InstructionClass expectedClass)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(expectedClass, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(3, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVL)]
    [InlineData(InstructionsEnum.VSETVLI)]
    [InlineData(InstructionsEnum.VSETIVLI)]
    public void PublishedVectorConfigDescriptors_UseOpcodeRegistryLatencyAndPreservePublishedDescriptorSurface(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.System, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
    }

    [Theory]
    [InlineData(InstructionsEnum.VADD, 1)]
    [InlineData(InstructionsEnum.VMUL, 3)]
    [InlineData(InstructionsEnum.VDIV, 16)]
    [InlineData(InstructionsEnum.VMOD, 16)]
    [InlineData(InstructionsEnum.VXOR, 1)]
    [InlineData(InstructionsEnum.VSLL, 1)]
    [InlineData(InstructionsEnum.VMINU, 1)]
    public void PublishedVectorBinaryDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode,
        byte expectedLatency)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(expectedLatency, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSQRT, 8)]
    [InlineData(InstructionsEnum.VNOT, 1)]
    [InlineData(InstructionsEnum.VPOPCNT, 2)]
    [InlineData(InstructionsEnum.VREVERSE, 2)]
    public void PublishedVectorUnaryDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode,
        byte expectedLatency)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(expectedLatency, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VCMPEQ)]
    [InlineData(InstructionsEnum.VCMPGE)]
    public void PublishedVectorComparisonDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VFMADD)]
    [InlineData(InstructionsEnum.VFNMSUB)]
    public void PublishedVectorFmaDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(4, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VREDSUM)]
    [InlineData(InstructionsEnum.VREDXOR)]
    public void PublishedVectorReductionDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(8, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VDOT)]
    [InlineData(InstructionsEnum.VDOT_FP8)]
    public void PublishedVectorDotProductDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(12, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VPERMUTE)]
    [InlineData(InstructionsEnum.VRGATHER)]
    public void PublishedVectorPermutationDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(4, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSLIDEUP)]
    [InlineData(InstructionsEnum.VSLIDEDOWN)]
    public void PublishedVectorSlideDescriptors_UseOpcodeRegistryLatencyAndPreserveVectorFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(3, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VMAND)]
    [InlineData(InstructionsEnum.VMOR)]
    [InlineData(InstructionsEnum.VMXOR)]
    [InlineData(InstructionsEnum.VMNOT)]
    public void PublishedVectorMaskDescriptors_UseOpcodeRegistryLatencyAndPreservePredicateFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(1, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(0, descriptor.MemFootprintClass);
    }

    [Fact]
    public void PublishedVectorMaskPopCountDescriptor_UsesOpcodeRegistryLatencyAndPreservesScalarWritebackFlags()
    {
        OpcodeInfo info = RequirePublishedInfo(InstructionsEnum.VPOPC);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VPOPC);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(2, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(descriptor.WritesRegister);
        Assert.Equal(0, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.VCOMPRESS)]
    [InlineData(InstructionsEnum.VEXPAND)]
    public void PublishedVectorPredicativeMovementDescriptors_UseOpcodeRegistryLatencyAndPreserveSingleSurfaceFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.True(info.IsVector);
        Assert.Equal(4, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
        Assert.Equal(2, descriptor.MemFootprintClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.ADDI)]
    [InlineData(InstructionsEnum.LUI)]
    [InlineData(InstructionsEnum.AUIPC)]
    public void PublishedScalarImmediateDescriptors_UseOpcodeRegistryLatencyAndPreserveScalarFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(descriptor.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.SLT)]
    [InlineData(InstructionsEnum.MULHU)]
    [InlineData(InstructionsEnum.REMU)]
    public void PublishedScalarRegisterDescriptors_UseOpcodeRegistryLatencyAndPreserveScalarFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(descriptor.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.Addition, 1)]
    [InlineData(InstructionsEnum.Subtraction, 1)]
    [InlineData(InstructionsEnum.Multiplication, 3)]
    [InlineData(InstructionsEnum.Division, 16)]
    public void PublishedBaseScalarDescriptors_UseOpcodeRegistryLatencyAndPreserveScalarFlags(
        InstructionsEnum opcode,
        byte expectedLatency)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.ScalarAlu, info.InstructionClass);
        Assert.Equal(expectedLatency, info.ExecutionLatency);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.True(descriptor.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.JAL)]
    [InlineData(InstructionsEnum.JALR)]
    [InlineData(InstructionsEnum.BEQ)]
    [InlineData(InstructionsEnum.BGEU)]
    public void PublishedBranchDescriptors_UseOpcodeRegistryLatencyAndPreservePublishedDescriptorSurface(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.ControlFlow, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
    }

    [Theory]
    [InlineData(InstructionsEnum.LB)]
    [InlineData(InstructionsEnum.LD)]
    public void PublishedTypedLoadDescriptors_UseOpcodeRegistryLatencyAndPreserveMemoryFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.Memory, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(1, descriptor.MemFootprintClass);
        Assert.True(descriptor.IsMemoryOp);
        Assert.True(descriptor.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.SW)]
    [InlineData(InstructionsEnum.SD)]
    public void PublishedTypedStoreDescriptors_UseOpcodeRegistryLatencyAndPreserveMemoryFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.Memory, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(1, descriptor.MemFootprintClass);
        Assert.True(descriptor.IsMemoryOp);
        Assert.False(descriptor.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.LR_W)]
    [InlineData(InstructionsEnum.SC_D)]
    [InlineData(InstructionsEnum.AMOADD_W)]
    public void PublishedAtomicDescriptors_UseOpcodeRegistryLatencyAndPreserveAtomicFlags(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.Atomic, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(1, descriptor.MemFootprintClass);
        Assert.True(descriptor.IsMemoryOp);
        Assert.True(descriptor.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.CSRRW)]
    [InlineData(InstructionsEnum.CSR_CLEAR)]
    [InlineData(InstructionsEnum.CSRRCI)]
    public void PublishedCsrDescriptors_UseOpcodeRegistryLatencyAndPreserveCanonicalOverrides(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
        Assert.False(descriptor.IsMemoryOp);
        Assert.Equal(opcode != InstructionsEnum.CSR_CLEAR, descriptor.WritesRegister);
    }

    [Theory]
    [InlineData(InstructionsEnum.VMXON)]
    [InlineData(InstructionsEnum.VMREAD)]
    [InlineData(InstructionsEnum.VMCLEAR)]
    [InlineData(InstructionsEnum.VMPTRLD)]
    public void PublishedVmxDescriptors_UseOpcodeRegistryLatencyInsteadOfManualRuntimeOverride(
        InstructionsEnum opcode)
    {
        OpcodeInfo info = RequirePublishedInfo(opcode);
        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);

        Assert.NotNull(descriptor);
        Assert.Equal(InstructionClass.Vmx, info.InstructionClass);
        Assert.Equal(info.ExecutionLatency, descriptor!.Latency);
    }

    private static OpcodeInfo RequirePublishedInfo(InstructionsEnum opcode)
    {
        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.True(info.HasValue, $"Opcode {opcode} should stay published in OpcodeRegistry.");
        return info.Value;
    }

    private static bool IsKnownLegacyDescriptorLatencyOverride(InstructionsEnum opcode)
    {
        return opcode == InstructionsEnum.Modulus;
    }
}
