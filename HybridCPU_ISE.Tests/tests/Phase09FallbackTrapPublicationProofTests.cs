using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase09FallbackTrapPublicationProofTests
{
    [Fact]
    public void BuildFallbackSlotCarrierBundle_CanonicalKnownMemoryTrap_PreservesOwnerPlacementAndBankIntent()
    {
        const ulong pc = 0x5D00;
        const ulong address = 0x6000;
        const int expectedBankIntent = 6;

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateInvalidOpcodeInstruction(),
                CreateRetainedLegacyLoadMoveInstruction(
                    VLIW_Instruction.NoArchReg,
                    address));

        var slotMetadata = new SlotMetadata
        {
            StealabilityPolicy = StealabilityPolicy.NotStealable,
            LocalityHint = LocalityHint.Cold,
            AdmissionMetadata = MicroOpAdmissionMetadata.Default with
            {
                OwnerContextId = 11,
                DomainTag = 0x24,
                Placement = new SlotPlacementMetadata
                {
                    RequiredSlotClass = SlotClass.AluClass,
                    PinningKind = SlotPinningKind.ClassFlexible,
                    PinnedLaneId = 0,
                    DomainTag = 0x24
                }
            }
        };

        var annotations = new VliwBundleAnnotations(
            new[]
            {
                new InstructionSlotMetadata(VtId.Create(3), SlotMetadata.Default),
                new InstructionSlotMetadata(VtId.Create(3), slotMetadata)
            });

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildFallbackCarrierBundleForTesting(
                rawSlots,
                RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder(),
                new InvalidOpcodeException(
                    "Synthetic bundle decode fault",
                    opcodeIdentifier: "synthetic",
                    slotIndex: 0,
                    isProhibited: false),
                pc,
                annotations);

        TrapMicroOp trapMicroOp = Assert.IsType<TrapMicroOp>(carrierBundle[1]);
        Assert.Equal((uint)InstructionsEnum.Load, trapMicroOp.OpCode);
        Assert.Equal(3, trapMicroOp.VirtualThreadId);
        Assert.Equal(3, trapMicroOp.OwnerThreadId);
        Assert.Equal(11, trapMicroOp.OwnerContextId);
        Assert.Equal(LocalityHint.Cold, trapMicroOp.MemoryLocalityHint);
        Assert.Equal(0x24UL, trapMicroOp.Placement.DomainTag);
        Assert.Equal(0x24UL, trapMicroOp.AdmissionMetadata.DomainTag);
        Assert.True(trapMicroOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(expectedBankIntent, trapMicroOp.ProjectedMemoryBankIntent);

        DecodedBundleTransportFacts transportFacts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(pc, carrierBundle);
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[1];

        Assert.IsType<TrapMicroOp>(slot.MicroOp);
        Assert.Equal(3, slot.VirtualThreadId);
        Assert.Equal(3, slot.OwnerThreadId);
        Assert.Equal((uint)InstructionsEnum.Load, slot.OpCode);
        Assert.True(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(SlotClass.LsuClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, slot.Placement.PinningKind);
        Assert.Equal(0x24UL, slot.Placement.DomainTag);
        Assert.Equal(expectedBankIntent, slot.MemoryBankIntent);
    }

    [Fact]
    public void BuildFallbackSlotCarrierBundle_NonIngressDecoderException_BubblesInsteadOfBecomingTrap()
    {
        const ulong pc = 0x5D40;

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateRetainedLegacyLoadMoveInstruction(
                    destinationRegister: 7,
                    address: 0x7000));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => DecodedBundleTransportProjector.BuildFallbackCarrierBundleForTesting(
                rawSlots,
                new ThrowingDecoder(),
                new InvalidOpcodeException(
                    "Synthetic bundle decode fault",
                    opcodeIdentifier: "synthetic",
                    slotIndex: 0,
                    isProhibited: false),
                pc));

        Assert.Contains("Synthetic internal decoder bug", ex.Message, StringComparison.Ordinal);
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] occupiedSlots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < occupiedSlots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = occupiedSlots[slotIndex];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateInvalidOpcodeInstruction()
    {
        return new VLIW_Instruction
        {
            OpCode = 14u,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0,
            Src2Pointer = 0,
            Immediate = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateRetainedLegacyLoadMoveInstruction(
        byte destinationRegister,
        ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Move,
            DataType = 3,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                destinationRegister,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private sealed class ThrowingDecoder : IDecoderFrontend
    {
        public InstructionIR Decode(in VLIW_Instruction instruction, int slotIndex)
        {
            throw new InvalidOperationException("Synthetic internal decoder bug");
        }

        public DecodedInstructionBundle DecodeInstructionBundle(
            ReadOnlySpan<VLIW_Instruction> bundle,
            VliwBundleAnnotations? bundleAnnotations,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            throw new NotSupportedException();
        }

        public DecodedInstructionBundle DecodeInstructionBundle(
            ReadOnlySpan<VLIW_Instruction> bundle,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            throw new NotSupportedException();
        }
    }
}


