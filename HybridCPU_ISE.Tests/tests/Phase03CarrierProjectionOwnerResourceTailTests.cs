using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using System.Linq;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03CarrierProjectionOwnerResourceTailTests
{
    private const ulong MemoryDomainMaskLow = 0xFFFFUL << 32;

    [Fact]
    public void LegacySlotCarrierMaterializer_LoadProjection_UsesCanonicalOwnerThreadForMemoryDomainResourceMask()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.LW, rd: 7, rs1: 2, immediate: 0x40));
        var annotations = CreateAnnotations(vtId: 2);

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots, annotations);
        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(2, microOp.VirtualThreadId);
        Assert.Equal(2, microOp.OwnerThreadId);
        Assert.Equal(
            ResourceMaskBuilder.ForMemoryDomain(2).Low,
            microOp.ResourceMask.Low & MemoryDomainMaskLow);
        Assert.Equal(
            0UL,
            microOp.ResourceMask.Low & ResourceMaskBuilder.ForMemoryDomain(0).Low);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_StoreProjection_UsesCanonicalOwnerThreadForMemoryDomainResourceMask()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.SW, rs1: 2, rs2: 7, immediate: 0x40));
        var annotations = CreateAnnotations(vtId: 3);

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots, annotations);
        StoreMicroOp microOp = Assert.IsType<StoreMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(3, microOp.VirtualThreadId);
        Assert.Equal(3, microOp.OwnerThreadId);
        Assert.Equal(
            ResourceMaskBuilder.ForMemoryDomain(3).Low,
            microOp.ResourceMask.Low & MemoryDomainMaskLow);
        Assert.Equal(
            0UL,
            microOp.ResourceMask.Low & ResourceMaskBuilder.ForMemoryDomain(0).Low);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_AtomicProjection_UsesCanonicalOwnerThreadForMemoryDomainResourceMask()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 7, rs1: 2, rs2: 6));
        var annotations = CreateAnnotations(vtId: 2);

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots, annotations);
        AtomicMicroOp microOp = Assert.IsType<AtomicMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(2, microOp.VirtualThreadId);
        Assert.Equal(2, microOp.OwnerThreadId);
        Assert.Equal(
            ResourceMaskBuilder.ForMemoryDomain(2).Low,
            microOp.ResourceMask.Low & MemoryDomainMaskLow);
        Assert.Equal(
            0UL,
            microOp.ResourceMask.Low & ResourceMaskBuilder.ForMemoryDomain(0).Low);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_DefaultOwnerContextProjection_PreservesDefaultArchitecturalContextZero()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 7, rs1: 2, rs2: 6));
        var annotations = CreateAnnotations(vtId: 3);

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots, annotations);
        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(3, microOp.VirtualThreadId);
        Assert.Equal(3, microOp.OwnerThreadId);
        Assert.Equal(0, microOp.OwnerContextId);
    }

    [Fact]
    public void TypedSlotScheduler_CanonicalProjection_DefaultOwnerContext_AllowsSameContextCrossVtInjection()
    {
        var scheduler = new MicroOpScheduler
        {
            TypedSlotEnabled = true
        };

        ScalarALUMicroOp owner = Assert.IsType<ScalarALUMicroOp>(
            DecodeAndMaterializeSingleSlot(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 5, rs1: 1, rs2: 2)),
                CreateAnnotations(vtId: 0)).MicroOp);
        ScalarALUMicroOp candidate = Assert.IsType<ScalarALUMicroOp>(
            DecodeAndMaterializeSingleSlot(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 13, rs1: 9, rs2: 10)),
                CreateAnnotations(vtId: 3)).MicroOp);

        Assert.Equal(0, owner.OwnerContextId);
        Assert.Equal(0, candidate.OwnerContextId);

        MicroOp[] ownerBundle = new MicroOp[8];
        ownerBundle[0] = owner;

        scheduler.NominateSmtCandidate(candidate.VirtualThreadId, candidate);
        MicroOp[] packed = scheduler.PackBundleIntraCoreSmt(
            ownerBundle,
            ownerVirtualThreadId: 0,
            localCoreId: 0);

        Assert.Contains(candidate, packed);
        Assert.Equal(1, packed.Count(op => op != null && !ReferenceEquals(op, owner)));
        Assert.True(scheduler.ClassFlexibleInjects >= 1);
        Assert.True(scheduler.NopAvoided >= 1);
        Assert.Equal(0, scheduler.SmtOwnerContextGuardRejects);
        Assert.Equal(0, scheduler.SmtSharedResourceCertificateRejects);
        Assert.Equal(0, scheduler.SmtRegisterGroupCertificateRejects);
        Assert.Equal(0, scheduler.SmtLegalityRejectByAluClass);
        Assert.Equal(0, scheduler.CertificateRejectByAluClass);
    }

    [Fact]
    public void TypedSlotScheduler_CanonicalProjection_DifferentOwnerContext_RejectsWithOwnerGuard()
    {
        var scheduler = new MicroOpScheduler
        {
            TypedSlotEnabled = true
        };

        ScalarALUMicroOp owner = Assert.IsType<ScalarALUMicroOp>(
            DecodeAndMaterializeSingleSlot(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 5, rs1: 1, rs2: 2)),
                CreateAnnotations(vtId: 0, ownerContextId: 0)).MicroOp);
        ScalarALUMicroOp candidate = Assert.IsType<ScalarALUMicroOp>(
            DecodeAndMaterializeSingleSlot(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 13, rs1: 9, rs2: 10)),
                CreateAnnotations(vtId: 3, ownerContextId: 7)).MicroOp);

        Assert.Equal(0, owner.OwnerContextId);
        Assert.Equal(7, candidate.OwnerContextId);

        MicroOp[] ownerBundle = new MicroOp[8];
        ownerBundle[0] = owner;

        scheduler.NominateSmtCandidate(candidate.VirtualThreadId, candidate);
        MicroOp[] packed = scheduler.PackBundleIntraCoreSmt(
            ownerBundle,
            ownerVirtualThreadId: 0,
            localCoreId: 0);

        Assert.DoesNotContain(candidate, packed);
        Assert.Equal(1, scheduler.SmtOwnerContextGuardRejects);
        Assert.Equal(0, scheduler.SmtSharedResourceCertificateRejects);
        Assert.Equal(0, scheduler.SmtRegisterGroupCertificateRejects);
        Assert.Equal(1, scheduler.SmtLegalityRejectByAluClass);
        Assert.Equal(0, scheduler.CertificateRejectByAluClass);
        Assert.Equal(RejectKind.OwnerMismatch, scheduler.LastSmtLegalityRejectKind);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, scheduler.LastSmtLegalityAuthoritySource);
    }

    [Fact]
    public void TypedSlotScheduler_CanonicalProjection_SameContextRegisterGroupConflict_RejectsWithCertificateOnly()
    {
        var scheduler = new MicroOpScheduler
        {
            TypedSlotEnabled = true
        };

        ScalarALUMicroOp owner = Assert.IsType<ScalarALUMicroOp>(
            DecodeAndMaterializeSingleSlot(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 5, rs1: 1, rs2: 2)),
                CreateAnnotations(vtId: 0, ownerContextId: 0)).MicroOp);
        ScalarALUMicroOp residentSameVt = Assert.IsType<ScalarALUMicroOp>(
            DecodeAndMaterializeSingleSlot(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 13, rs1: 9, rs2: 10)),
                CreateAnnotations(vtId: 3, ownerContextId: 0)).MicroOp);
        ScalarALUMicroOp candidate = Assert.IsType<ScalarALUMicroOp>(
            DecodeAndMaterializeSingleSlot(
                CreateBundle(CreateScalarInstruction(InstructionsEnum.Addition, rd: 14, rs1: 11, rs2: 12)),
                CreateAnnotations(vtId: 3, ownerContextId: 0)).MicroOp);

        Assert.Equal(0, owner.OwnerContextId);
        Assert.Equal(0, residentSameVt.OwnerContextId);
        Assert.Equal(0, candidate.OwnerContextId);

        MicroOp[] ownerBundle = new MicroOp[8];
        ownerBundle[0] = owner;
        ownerBundle[1] = residentSameVt;

        scheduler.NominateSmtCandidate(candidate.VirtualThreadId, candidate);
        MicroOp[] packed = scheduler.PackBundleIntraCoreSmt(
            ownerBundle,
            ownerVirtualThreadId: 0,
            localCoreId: 0);

        Assert.DoesNotContain(candidate, packed);
        Assert.Equal(0, scheduler.SmtOwnerContextGuardRejects);
        Assert.Equal(0, scheduler.SmtSharedResourceCertificateRejects);
        Assert.Equal(1, scheduler.SmtRegisterGroupCertificateRejects);
        Assert.Equal(1, scheduler.SmtLegalityRejectByAluClass);
        Assert.Equal(1, scheduler.CertificateRejectByAluClass);
        Assert.Equal(RejectKind.CrossLaneConflict, scheduler.LastSmtLegalityRejectKind);
        Assert.Equal(LegalityAuthoritySource.StructuralCertificate, scheduler.LastSmtLegalityAuthoritySource);
    }

    private static DecodedBundleSlotDescriptor DecodeAndMaterializeSingleSlot(
        VLIW_Instruction[] rawSlots,
        VliwBundleAnnotations annotations)
    {
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, annotations, bundleAddress: 0x4000, bundleSerial: 29);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return DecodedBundleSlotDescriptor.Create(0, Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]));
    }

    private static VliwBundleAnnotations CreateAnnotations(byte vtId, int? ownerContextId = null)
    {
        SlotMetadata slotMetadata = ownerContextId.HasValue
            ? SlotMetadata.Default with
            {
                AdmissionMetadata = MicroOpAdmissionMetadata.Default with
                {
                    OwnerContextId = ownerContextId.Value
                }
            }
            : SlotMetadata.Default;

        return new VliwBundleAnnotations(
            new[]
            {
                new InstructionSlotMetadata(VtId.Create(vtId), slotMetadata)
            });
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


