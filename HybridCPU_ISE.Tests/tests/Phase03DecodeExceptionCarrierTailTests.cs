using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DecodeExceptionCarrierTailTests
{
    [Fact]
    public void DecodeExceptionOrdering_UsesAdmissionDomainAndLiveExecutionHeader_WhenCarrierFactsAreTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4000);
        core.CsrMemDomainCert = 0x2;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 1));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x4000, bundleSerial: 64);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        MicroOp microOp = Assert.IsAssignableFrom<MicroOp>(canonicalSlot.MicroOp);

        microOp.VirtualThreadId = 2;
        microOp.IsFspInjected = true;
        microOp.Placement = microOp.Placement with { DomainTag = 0x8 };
        microOp.RefreshAdmissionMetadata();

        DecodedBundleSlotDescriptor tamperedSlot = WithDecodeExceptionFacts(
            canonicalSlot,
            virtualThreadId: 1,
            placement: canonicalSlot.Placement with { DomainTag = 0x2 },
            isFspInjected: false);

        var decision = core.TestResolveDecodeExceptionOrderingDecision(tamperedSlot, faultingPc: 0x4000);

        Assert.True(decision.IsSilentSpeculativeSquash);
        Assert.False(decision.IsPreciseArchitecturalFault);
        Assert.Equal(2, decision.VirtualThreadId);
        Assert.Equal(0x4000UL, decision.FaultingPC);
        Assert.Equal(0x8UL, decision.OperationDomainTag);
        Assert.Equal(0x2UL, decision.ActiveCert);
    }

    [Fact]
    public void DecodeExceptionOrdering_UsesLiveFspFlag_WhenCarrierClaimsInjected()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x5000);
        core.CsrMemDomainCert = 0x2;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 1));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x5000, bundleSerial: 65);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor canonicalSlot = transportFacts.Slots[0];
        MicroOp microOp = Assert.IsAssignableFrom<MicroOp>(canonicalSlot.MicroOp);

        microOp.VirtualThreadId = 2;
        microOp.IsFspInjected = false;
        microOp.Placement = microOp.Placement with { DomainTag = 0x8 };
        microOp.RefreshAdmissionMetadata();

        DecodedBundleSlotDescriptor tamperedSlot = WithDecodeExceptionFacts(
            canonicalSlot,
            virtualThreadId: 1,
            placement: canonicalSlot.Placement with { DomainTag = 0x8 },
            isFspInjected: true);

        var decision = core.TestResolveDecodeExceptionOrderingDecision(tamperedSlot, faultingPc: 0x5000);

        Assert.False(decision.IsSilentSpeculativeSquash);
        Assert.True(decision.IsPreciseArchitecturalFault);
        Assert.Equal(2, decision.VirtualThreadId);
        Assert.Equal(0x5000UL, decision.FaultingPC);
        Assert.Equal(0x8UL, decision.OperationDomainTag);
        Assert.Equal(0x2UL, decision.ActiveCert);
    }

    private static DecodedBundleSlotDescriptor WithDecodeExceptionFacts(
        in DecodedBundleSlotDescriptor slot,
        int virtualThreadId,
        SlotPlacementMetadata placement,
        bool isFspInjected)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            virtualThreadId,
            slot.OwnerThreadId,
            slot.OpCode,
            slot.ReadRegisters,
            slot.WriteRegisters,
            slot.WritesRegister,
            slot.IsMemoryOp,
            slot.IsControlFlow,
            placement,
            slot.MemoryBankIntent,
            isFspInjected,
            slot.IsEmptyOrNop);
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}

