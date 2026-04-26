using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorDotProductPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.VDOT, DataTypeEnum.INT32, 2, 4, 8UL, 4UL)]
    [InlineData(InstructionsEnum.VDOTU, DataTypeEnum.UINT32, 2, 4, 8UL, 4UL)]
    [InlineData(InstructionsEnum.VDOTF, DataTypeEnum.FLOAT32, 2, 4, 8UL, 4UL)]
    [InlineData(InstructionsEnum.VDOT_FP8, DataTypeEnum.FLOAT8_E4M3, 4, 1, 4UL, 4UL)]
    public void DirectFactoryVectorDotProduct_PublishesScalarFootprintBeforeManualPublication(
        InstructionsEnum opcode,
        DataTypeEnum dataType,
        ushort streamLength,
        ushort stride,
        ulong expectedReadBytesPerOperand,
        ulong expectedWriteBytes)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA900);

        VectorDotProductMicroOp microOp =
            CreateDirectFactoryVectorDotProductMicroOp(opcode, dataType, streamLength, stride);

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
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x340UL, expectedReadBytesPerOperand), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x3C0UL, expectedReadBytesPerOperand), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x340UL, expectedWriteBytes), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static VectorDotProductMicroOp CreateDirectFactoryVectorDotProductMicroOp(
        InstructionsEnum opcode,
        DataTypeEnum dataType,
        ushort streamLength,
        ushort stride)
    {
        VLIW_Instruction instruction =
            CreateVectorInstruction(opcode, dataType, streamLength, stride);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorDotProductMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorInstruction(
        InstructionsEnum opcode,
        DataTypeEnum dataType,
        ushort streamLength,
        ushort stride)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = dataType,
            PredicateMask = 0x0F,
            DestSrc1Pointer = 0x340,
            Src2Pointer = 0x3C0,
            StreamLength = streamLength,
            Stride = stride,
            VirtualThreadId = 0
        };
    }
}

