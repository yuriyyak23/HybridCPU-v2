using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorFmaPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.VFMADD)]
    [InlineData(InstructionsEnum.VFMSUB)]
    [InlineData(InstructionsEnum.VFNMADD)]
    [InlineData(InstructionsEnum.VFNMSUB)]
    public void DirectFactoryVectorFma_PublishesDedicatedNonMemoryComputeCarrierBeforeManualPublication(
        InstructionsEnum opcode)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedTriOpDescriptor(descriptorAddress: 0x340, srcAPointer: 0x440, srcBPointer: 0x540, strideA: 4, strideB: 4);

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xB000);

        VectorFmaMicroOp microOp = CreateDirectFactoryVectorFmaMicroOp(opcode);

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.True(slot.IsVectorOp);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.WriteRegisters);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(3, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x240UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x440UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x540UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[2]);
        Assert.Equal((0x240UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static void InitializeCpuMainMemoryIdentityMap()
    {
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }

    private static void InitializeMemorySubsystem()
    {
        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void SeedTriOpDescriptor(
        ulong descriptorAddress,
        ulong srcAPointer,
        ulong srcBPointer,
        ushort strideA,
        ushort strideB)
    {
        byte[] descriptor = new byte[20];
        BitConverter.GetBytes(srcAPointer).CopyTo(descriptor, 0);
        BitConverter.GetBytes(srcBPointer).CopyTo(descriptor, 8);
        BitConverter.GetBytes(strideA).CopyTo(descriptor, 16);
        BitConverter.GetBytes(strideB).CopyTo(descriptor, 18);
        Processor.MainMemory.WriteToPosition(descriptor, descriptorAddress);
    }

    private static VectorFmaMicroOp CreateDirectFactoryVectorFmaMicroOp(InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateVectorInstruction(opcode);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorFmaMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.FLOAT32,
            PredicateMask = 0x07,
            DestSrc1Pointer = 0x240,
            Src2Pointer = 0x340,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 0
        };
    }
}

