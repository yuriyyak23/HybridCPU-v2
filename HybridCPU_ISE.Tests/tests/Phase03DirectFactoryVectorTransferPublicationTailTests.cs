using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorTransferPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.VLOAD, 0x200UL, 0x300UL)]
    [InlineData(InstructionsEnum.VSTORE, 0x280UL, 0x380UL)]
    public void DirectFactoryVectorTransfer_PublishesCanonicalNonMemoryTwoSurfaceTransferShapeBeforeManualPublication(
        InstructionsEnum opcode,
        ulong destSrc1Pointer,
        ulong src2Pointer)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xAF00);

        VectorTransferMicroOp microOp = CreateDirectFactoryVectorTransferMicroOp(
            opcode,
            destSrc1Pointer,
            src2Pointer);

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
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);

        switch (opcode)
        {
            case InstructionsEnum.VLOAD:
                Assert.Equal((src2Pointer, 16UL), Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges));
                Assert.Equal((destSrc1Pointer, 16UL), Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges));
                break;

            case InstructionsEnum.VSTORE:
                Assert.Equal((destSrc1Pointer, 16UL), Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges));
                Assert.Equal((src2Pointer, 16UL), Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges));
                break;

            default:
                throw new InvalidOperationException($"Unexpected vector transfer opcode {opcode}.");
        }
    }

    private static VectorTransferMicroOp CreateDirectFactoryVectorTransferMicroOp(
        InstructionsEnum opcode,
        ulong destSrc1Pointer,
        ulong src2Pointer)
    {
        VLIW_Instruction instruction = CreateVectorInstruction(
            opcode,
            destSrc1Pointer,
            src2Pointer);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorTransferMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorInstruction(
        InstructionsEnum opcode,
        ulong destSrc1Pointer,
        ulong src2Pointer)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0x0F,
            DestSrc1Pointer = destSrc1Pointer,
            Src2Pointer = src2Pointer,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 0
        };
    }
}

