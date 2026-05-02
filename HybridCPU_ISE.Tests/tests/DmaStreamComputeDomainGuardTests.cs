using System;
using System.Buffers.Binary;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeDomainGuardTests
{
    [Fact]
    public void StructuralOwnerRead_LocatesOwnerFieldsWithoutAcceptingExecutableDescriptor()
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt64LittleEndian(
            descriptorBytes.AsSpan(HeaderSize + RangeLengthFieldOffset),
            0);

        DmaStreamComputeStructuralReadResult structural =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
        DmaStreamComputeValidationResult unguarded =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes);

        Assert.True(structural.IsValid, structural.Message);
        Assert.NotNull(structural.OwnerBinding);
        Assert.Equal(OwnerVirtualThreadId, structural.OwnerBinding.OwnerVirtualThreadId);
        Assert.Equal(OwnerContextId, structural.OwnerBinding.OwnerContextId);
        Assert.Equal(OwnerCoreId, structural.OwnerBinding.OwnerCoreId);
        Assert.Equal(OwnerPodId, structural.OwnerBinding.OwnerPodId);
        Assert.Equal(OwnerDomainTag, structural.OwnerBinding.OwnerDomainTag);
        Assert.False(unguarded.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, unguarded.Fault);
        Assert.Null(unguarded.Descriptor);
    }

    [Fact]
    public void OwnerVirtualThreadMismatch_RejectsBeforeReplayReuseAndDescriptorValidation()
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt64LittleEndian(
            descriptorBytes.AsSpan(HeaderSize + RangeLengthFieldOffset),
            0);
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext with { OwnerVirtualThreadId = 3 });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);

        Assert.False(guardDecision.IsAllowed);
        Assert.Equal(RejectKind.OwnerMismatch, guardDecision.LegalityDecision.RejectKind);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, guardDecision.LegalityDecision.AuthoritySource);
        Assert.False(guardDecision.LegalityDecision.AttemptedReplayCertificateReuse);
        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, result.Fault);
        Assert.DoesNotContain("length", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OwnerContextMismatch_RejectsBeforeCertificateAcceptance()
    {
        byte[] descriptorBytes = BuildDescriptor();
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext with { OwnerContextId = 999 });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, result.Fault);
        Assert.Equal(RejectKind.OwnerMismatch, guardDecision.LegalityDecision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, guardDecision.LegalityDecision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, guardDecision.LegalityDecision.AuthoritySource);
        Assert.False(guardDecision.LegalityDecision.AttemptedReplayCertificateReuse);
    }

    [Fact]
    public void DomainMismatch_RejectsBeforeCertificateAcceptance()
    {
        byte[] descriptorBytes = BuildDescriptor();
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext with { OwnerDomainTag = 0x400 });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, result.Fault);
        Assert.Equal(RejectKind.DomainMismatch, guardDecision.LegalityDecision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, guardDecision.LegalityDecision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, guardDecision.LegalityDecision.AuthoritySource);
        Assert.False(guardDecision.LegalityDecision.AttemptedReplayCertificateReuse);
    }

    [Fact]
    public void ActiveDomainCertificateMismatch_RejectsBeforeCertificateAcceptance()
    {
        byte[] descriptorBytes = BuildDescriptor();
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext with { ActiveDomainCertificate = 0x400 });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, result.Fault);
        Assert.Equal(RejectKind.DomainMismatch, guardDecision.LegalityDecision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, guardDecision.LegalityDecision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, guardDecision.LegalityDecision.AuthoritySource);
        Assert.False(guardDecision.LegalityDecision.AttemptedReplayCertificateReuse);
        Assert.Contains("certificate", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoreOrPodMismatch_RejectsBeforeCrossCoreReuse()
    {
        byte[] descriptorBytes = BuildDescriptor();
        DmaStreamComputeOwnerGuardDecision coreMismatch =
            EvaluateGuard(descriptorBytes, ValidContext with { OwnerCoreId = 9 });
        DmaStreamComputeOwnerGuardDecision podMismatch =
            EvaluateGuard(descriptorBytes, ValidContext with { OwnerPodId = 7 });

        Assert.False(coreMismatch.IsAllowed);
        Assert.False(podMismatch.IsAllowed);
        Assert.Equal(RejectKind.OwnerMismatch, coreMismatch.LegalityDecision.RejectKind);
        Assert.Equal(RejectKind.OwnerMismatch, podMismatch.LegalityDecision.RejectKind);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, coreMismatch.LegalityDecision.AuthoritySource);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, podMismatch.LegalityDecision.AuthoritySource);
        Assert.False(coreMismatch.LegalityDecision.AttemptedReplayCertificateReuse);
        Assert.False(podMismatch.LegalityDecision.AttemptedReplayCertificateReuse);
    }

    [Fact]
    public void DescriptorDeviceId_CannotBypassOwnerDomainGuard()
    {
        byte[] descriptorBytes = BuildDescriptor(deviceId: 99);
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext with { DeviceId = 99 });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);

        Assert.False(guardDecision.IsAllowed);
        Assert.Equal(RejectKind.OwnerMismatch, guardDecision.LegalityDecision.RejectKind);
        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, result.Fault);
        Assert.Contains("DeviceId", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawVirtualThreadHint_CannotSatisfyDescriptorOwnerBindingByItself()
    {
        byte[] descriptorBytes = BuildDescriptor(ownerVirtualThreadId: 0);
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP,
            VirtualThreadId = 3
        };
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext with { OwnerVirtualThreadId = rawInstruction.VirtualThreadId });

        DmaStreamComputeValidationResult rawCarrier =
            DmaStreamComputeDescriptorParser.TryDecodeRawVliwCarrier(in rawInstruction, slotIndex: 6);
        DmaStreamComputeValidationResult guardedParse =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);

        Assert.False(rawCarrier.IsValid);
        Assert.Contains("word3[49:48]", rawCarrier.Message, StringComparison.Ordinal);
        Assert.False(guardDecision.IsAllowed);
        Assert.False(guardedParse.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, guardedParse.Fault);
    }

    [Fact]
    public void ReplayCertificateIdentity_CannotBecomeOwnerDomainAuthority()
    {
        byte[] descriptorBytes = BuildDescriptor(ownerContextId: OwnerContextId + 1);
        DmaStreamComputeOwnerGuardDecision staleAllowDecision =
            DmaStreamComputeOwnerGuardDecision.Allow(
                BuildOwnerBinding(),
                ValidContext,
                "stale certificate-like identity is not owner authority");

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, staleAllowDecision);

        Assert.True(staleAllowDecision.IsAllowed);
        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, result.Fault);
        Assert.Contains("does not match", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuardedDescriptorAcceptance_PublishesMicroOpAndExecutionRemainsFailClosed()
    {
        byte[] descriptorBytes = BuildDescriptor();
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);
        DmaStreamComputeDescriptor descriptor = result.RequireDescriptorForAdmission();
        DmaStreamComputeMicroOp microOp = new(descriptor);
        var core = new Processor.CPU_Core(0);

        Assert.True(guardDecision.IsAllowed);
        Assert.True(result.IsValid, result.Message);
        Assert.Equal(guardDecision, descriptor.OwnerGuardDecision);
        Assert.Equal((int)OwnerVirtualThreadId, microOp.OwnerThreadId);
        Assert.Equal((int)OwnerVirtualThreadId, microOp.VirtualThreadId);
        Assert.Equal((int)OwnerContextId, microOp.OwnerContextId);
        Assert.Equal(OwnerDomainTag, microOp.Placement.DomainTag);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => microOp.Execute(ref core));
        Assert.Contains("execution is disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DmaStreamComputeGuardReject_UsesExistingTelemetryClassificationWithoutEnumRenumbering()
    {
        var verifier = new SafetyVerifier();
        DmaStreamComputeMicroOp candidate = CreateGuardedMicroOp();
        MicroOp ownerOperation = CreateGuardedMicroOp();
        var bundleCertificate = BundleResourceCertificate4Way.Empty;
        bundleCertificate.AddOperation(ownerOperation);
        SmtBundleMetadata4Way bundleMetadata =
            SmtBundleMetadata4Way.Empty(ownerVirtualThreadId: OwnerVirtualThreadId)
                .WithOperation(ownerOperation);
        SmtBundleMetadata4Way mismatchedMetadata = new(
            bundleMetadata.OwnerVirtualThreadId,
            (int)OwnerContextId + 1,
            bundleMetadata.OwnerDomainTag,
            bundleMetadata.BundleDomainXor,
            bundleMetadata.BundleDomainSum,
            bundleMetadata.OperationCount);
        var replayPhase = new ReplayPhaseContext(
            isActive: true,
            epochId: 101,
            cachedPc: 0x4400,
            epochLength: 8,
            completedReplays: 2,
            validSlotCount: 1,
            stableDonorMask: 0b0100_0000,
            lastInvalidationReason: ReplayPhaseInvalidationReason.None);
        PhaseCertificateTemplateKey4Way liveTemplateKey = new(
            replayPhase.Key,
            bundleCertificate.StructuralIdentity,
            mismatchedMetadata,
            BoundaryGuardState.Open(3));
        PhaseCertificateTemplate4Way phaseTemplate = new(liveTemplateKey, bundleCertificate);

        LegalityDecision decision = verifier.EvaluateSmtLegality(
            bundleCertificate,
            liveTemplateKey,
            phaseTemplate,
            candidate);

        Assert.Equal(0, (byte)TypedSlotRejectReason.None);
        Assert.Equal(4, (byte)TypedSlotRejectReason.DomainReject);
        Assert.Equal(20, (byte)TypedSlotRejectReason.FairnessDeferred);
        Assert.False(decision.IsAllowed);
        Assert.Equal(RejectKind.OwnerMismatch, decision.RejectKind);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, decision.AuthoritySource);
        Assert.False(decision.AttemptedReplayCertificateReuse);
    }

    private const ulong IdentityHash = 0xA11CE5EEDUL;
    private const ushort OwnerVirtualThreadId = 1;
    private const uint OwnerContextId = 77;
    private const uint OwnerCoreId = 2;
    private const uint OwnerPodId = 3;
    private const ulong OwnerDomainTag = 0x20;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;
    private const int RangeLengthFieldOffset = 8;

    private static readonly DmaStreamComputeOwnerGuardContext ValidContext = new(
        ownerVirtualThreadId: OwnerVirtualThreadId,
        ownerContextId: OwnerContextId,
        ownerCoreId: OwnerCoreId,
        ownerPodId: OwnerPodId,
        ownerDomainTag: OwnerDomainTag,
        activeDomainCertificate: OwnerDomainTag);

    private static DmaStreamComputeMicroOp CreateGuardedMicroOp()
    {
        byte[] descriptorBytes = BuildDescriptor();
        DmaStreamComputeOwnerGuardDecision guardDecision =
            EvaluateGuard(descriptorBytes, ValidContext);
        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision);
        Assert.True(result.IsValid, result.Message);
        return new DmaStreamComputeMicroOp(result.RequireDescriptorForAdmission());
    }

    private static DmaStreamComputeOwnerGuardDecision EvaluateGuard(
        byte[] descriptorBytes,
        DmaStreamComputeOwnerGuardContext context)
    {
        DmaStreamComputeStructuralReadResult structural =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
        Assert.True(structural.IsValid, structural.Message);
        return new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(
            structural.RequireOwnerBindingForGuard(),
            context);
    }

    private static DmaStreamComputeOwnerBinding BuildOwnerBinding(
        ushort ownerVirtualThreadId = OwnerVirtualThreadId,
        uint ownerContextId = OwnerContextId,
        uint ownerCoreId = OwnerCoreId,
        uint ownerPodId = OwnerPodId,
        uint deviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId,
        ulong ownerDomainTag = OwnerDomainTag) =>
        new()
        {
            OwnerVirtualThreadId = ownerVirtualThreadId,
            OwnerContextId = ownerContextId,
            OwnerCoreId = ownerCoreId,
            OwnerPodId = ownerPodId,
            DeviceId = deviceId,
            OwnerDomainTag = ownerDomainTag
        };

    private static byte[] BuildDescriptor(
        ushort ownerVirtualThreadId = OwnerVirtualThreadId,
        uint ownerContextId = OwnerContextId,
        uint ownerCoreId = OwnerCoreId,
        uint ownerPodId = OwnerPodId,
        uint deviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId,
        ulong ownerDomainTag = OwnerDomainTag)
    {
        const ushort sourceRangeCount = 2;
        const ushort destinationRangeCount = 1;
        const int sourceRangeTableOffset = HeaderSize;
        const int destinationRangeTableOffset = HeaderSize + (sourceRangeCount * RangeEntrySize);
        uint totalSize = (uint)(HeaderSize + ((sourceRangeCount + destinationRangeCount) * RangeEntrySize));
        byte[] bytes = new byte[totalSize];

        WriteUInt32(bytes, 0, DmaStreamComputeDescriptorParser.Magic);
        WriteUInt16(bytes, 4, DmaStreamComputeDescriptorParser.CurrentAbiVersion);
        WriteUInt16(bytes, 6, HeaderSize);
        WriteUInt32(bytes, 8, totalSize);
        WriteUInt64(bytes, 24, IdentityHash);
        WriteUInt64(bytes, 32, 0xC011EC7EUL);
        WriteUInt16(bytes, 40, (ushort)DmaStreamComputeOperationKind.Add);
        WriteUInt16(bytes, 42, (ushort)DmaStreamComputeElementType.UInt32);
        WriteUInt16(bytes, 44, (ushort)DmaStreamComputeShapeKind.Contiguous1D);
        WriteUInt16(bytes, 46, (ushort)DmaStreamComputeRangeEncoding.InlineContiguous);
        WriteUInt16(bytes, 48, sourceRangeCount);
        WriteUInt16(bytes, 50, destinationRangeCount);
        WriteUInt16(bytes, 56, (ushort)DmaStreamComputePartialCompletionPolicy.AllOrNone);
        WriteUInt16(bytes, 60, ownerVirtualThreadId);
        WriteUInt32(bytes, 64, ownerContextId);
        WriteUInt32(bytes, 68, ownerCoreId);
        WriteUInt32(bytes, 72, ownerPodId);
        WriteUInt32(bytes, 76, deviceId);
        WriteUInt64(bytes, 80, ownerDomainTag);
        WriteUInt32(bytes, 96, sourceRangeTableOffset);
        WriteUInt32(bytes, 100, destinationRangeTableOffset);

        WriteRange(bytes, sourceRangeTableOffset, 0x1000, 16);
        WriteRange(bytes, sourceRangeTableOffset + RangeEntrySize, 0x2000, 16);
        WriteRange(bytes, destinationRangeTableOffset, 0x9000, 16);
        return bytes;
    }

    private static void WriteRange(byte[] bytes, int offset, ulong address, ulong length)
    {
        WriteUInt64(bytes, offset, address);
        WriteUInt64(bytes, offset + RangeLengthFieldOffset, length);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);
}
