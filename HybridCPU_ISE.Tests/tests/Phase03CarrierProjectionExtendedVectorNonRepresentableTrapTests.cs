using System;
using HybridCPU_ISE.Arch;

using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03CarrierProjectionExtendedVectorNonRepresentableTrapTests
{
    [Theory]
    [MemberData(
        nameof(DeferredVectorBatchTestHelper.ExtendedNonRepresentableContours),
        MemberType = typeof(DeferredVectorBatchTestHelper))]
    public void LegacySlotCarrierMaterializer_WhenExtendedVectorFamilyUsesNonRepresentableIndexedOr2DContour_ThenProjectsTrapCarrier(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        const ulong pc = 0x8D00;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc);

        VLIW_Instruction[] rawSlots =
            DeferredVectorBatchTestHelper.CreateBundle(
                DeferredVectorBatchTestHelper.CreateAddressingInstruction(family, opcode, is2D));

        core.TestDecodeFetchedBundle(rawSlots, pc);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(slot.MicroOp);

        Assert.False(canonicalBundle.HasDecodeFault);
        Assert.False(canonicalBundle.IsEmpty);
        Assert.False(legalityDescriptor.HasDecodeFault);
        Assert.False(legalityDescriptor.IsEmpty);
        Assert.Equal((byte)0b0000_0001, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.True(slot.IsVectorOp);
        bool usesMemoryLikeTrapCarrier = family == DeferredVectorAddressingFamily.Transfer;
        Assert.Equal(usesMemoryLikeTrapCarrier, slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.Equal(
            DeferredVectorBatchTestHelper.ExpectsRegisterWrite(family),
            slot.WritesRegister);
        Assert.Equal(
            DeferredVectorBatchTestHelper.GetExpectedWriteRegisters(family),
            slot.WriteRegisters);
        Assert.Equal((uint)opcode, slot.OpCode);
        Assert.Equal(
            usesMemoryLikeTrapCarrier ? SlotClass.LsuClass : SlotClass.AluClass,
            slot.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, slot.Placement.PinningKind);
        Assert.Contains("Decode projection fault:", trapMicroOp.TrapReason, StringComparison.Ordinal);
        Assert.Contains(addressingContour, trapMicroOp.TrapReason, StringComparison.Ordinal);
        Assert.Contains(
            DeferredVectorBatchTestHelper.GetFactoryAddressingLabel(family),
            trapMicroOp.TrapReason,
            StringComparison.Ordinal);
    }
}

