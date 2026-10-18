using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcOwnerDomainGuardTests
{
    [Fact]
    public void DescriptorAcceptanceWithoutGuard_RejectsButStructuralReadRemainsEvidenceOnly()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();

        AcceleratorDescriptorStructuralReadResult structural =
            AcceleratorDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
        AcceleratorDescriptorValidationResult unguarded =
            AcceleratorDescriptorParser.Parse(
                descriptorBytes,
                L7SdcTestDescriptorFactory.CreateReference(descriptorBytes));

        Assert.True(structural.IsValid, structural.Message);
        Assert.NotNull(structural.OwnerBinding);
        Assert.Equal(L7SdcTestDescriptorFactory.OwnerVirtualThreadId, structural.OwnerBinding!.OwnerVirtualThreadId);
        Assert.Equal(L7SdcTestDescriptorFactory.OwnerContextId, structural.OwnerBinding.OwnerContextId);
        Assert.Equal(L7SdcTestDescriptorFactory.OwnerDomainTag, structural.OwnerBinding.DomainTag);
        Assert.False(unguarded.IsValid);
        Assert.Null(unguarded.Descriptor);
        Assert.Equal(AcceleratorDescriptorFault.OwnerDomainFault, unguarded.Fault);
    }

    [Fact]
    public void GuardBackedDescriptorAcceptance_SucceedsOnlyWhenEvidenceMatchesOwnerBinding()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorDescriptorValidationResult accepted =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        AcceleratorOwnerBinding mismatchedOwner =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(ownerContextId: 999);
        AcceleratorDescriptorValidationResult rejected =
            L7SdcTestDescriptorFactory.ParseWithGuard(
                descriptorBytes,
                L7SdcTestDescriptorFactory.CreateGuardEvidence(mismatchedOwner));

        Assert.True(accepted.IsValid, accepted.Message);
        Assert.True(accepted.RequireDescriptor().OwnerGuardDecision.IsAllowed);
        Assert.Equal(
            AcceleratorGuardSurface.DescriptorAcceptance,
            accepted.RequireDescriptor().OwnerGuardDecision.Surface);

        Assert.False(rejected.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.OwnerDomainFault, rejected.Fault);
        Assert.Contains("owner", rejected.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OwnerDomainMismatch_RejectsBeforeBackendQueueTokenOrCommitSurfaces()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt64(
            descriptorBytes,
            L7SdcTestDescriptorFactory.HeaderSize + L7SdcTestDescriptorFactory.RangeLengthFieldOffset,
            0);
        AcceleratorOwnerBinding mismatchedOwner =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(ownerVirtualThreadId: 3);

        AcceleratorDescriptorValidationResult rejected =
            L7SdcTestDescriptorFactory.ParseWithGuard(
                descriptorBytes,
                L7SdcTestDescriptorFactory.CreateGuardEvidence(mismatchedOwner));

        Assert.False(rejected.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.OwnerDomainFault, rejected.Fault);
        Assert.DoesNotContain("length", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("backend", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("queue", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("commit", rejected.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaleGuardDecision_CannotAuthorizeDifferentDescriptorOwnerBinding()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding staleOwner =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(ownerContextId: 999);
        AcceleratorGuardEvidence staleEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(staleOwner);
        AcceleratorGuardDecision staleAllowDecision =
            AcceleratorGuardDecision.Allow(
                AcceleratorGuardSurface.DescriptorAcceptance,
                staleOwner,
                staleEvidence,
                "stale guard-plane looking decision is not descriptor authority");

        AcceleratorDescriptorValidationResult result =
            AcceleratorDescriptorParser.Parse(
                descriptorBytes,
                staleAllowDecision,
                L7SdcTestDescriptorFactory.CreateReference(descriptorBytes));

        Assert.True(staleAllowDecision.IsAllowed);
        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.OwnerDomainFault, result.Fault);
        Assert.Contains("does not match", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CapabilityAcceptanceWithoutGuard_RejectsAndRegistrySuccessIsNotAuthority()
    {
        AcceleratorCapabilityRegistry registry = L7SdcCapabilityRegistryTests.CreateRegistry();
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();

        AcceleratorCapabilityQueryResult query =
            registry.Query("matmul.fixture.v1");
        AcceleratorCapabilityAcceptanceResult unguarded =
            registry.AcceptCapability("matmul.fixture.v1", descriptor.OwnerBinding);
        AcceleratorCapabilityAcceptanceResult guarded =
            registry.AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);

        Assert.True(query.IsMetadataAvailable);
        Assert.False(query.GrantsCommandSubmissionAuthority);
        Assert.True(unguarded.IsRejected);
        Assert.Equal(AcceleratorGuardFault.CapabilityGuardMissing, unguarded.GuardDecision.Fault);
        Assert.Contains("registry metadata is not authority", unguarded.RejectReason, StringComparison.OrdinalIgnoreCase);

        Assert.True(guarded.IsAccepted);
        Assert.NotNull(guarded.Descriptor);
        Assert.False(guarded.GrantsCommandSubmissionAuthority);
        Assert.False(guarded.GrantsExecutionAuthority);
        Assert.False(guarded.GrantsCommitAuthority);
    }

    [Fact]
    public void SubmitWithoutGuard_RejectsEvenWithStructurallyValidDescriptorSideband()
    {
        AcceleratorCommandDescriptor guardBacked =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCommandDescriptor unguarded = guardBacked with
        {
            OwnerGuardDecision = default
        };

        InvalidOperationException constructorReject =
            Assert.Throws<InvalidOperationException>(() => new AcceleratorSubmitMicroOp(unguarded));
        Assert.Contains("guard-backed", constructorReject.Message, StringComparison.OrdinalIgnoreCase);

        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);
        var metadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        for (int index = 0; index < metadata.Length; index++)
        {
            metadata[index] = InstructionSlotMetadata.Default;
        }

        metadata[7] = new InstructionSlotMetadata(
            VtId.Create(0),
            L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata())
        {
            AcceleratorCommandDescriptor = unguarded
        };

        InvalidOpcodeException decodeReject = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                new VliwBundleAnnotations(metadata),
                bundleAddress: 0xD500));

        Assert.Contains("guard-backed", decodeReject.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(DmaStreamComputeMicroOp), decodeReject.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(GenericMicroOp), decodeReject.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeviceExecutionAuthorizationWithoutGuard_RejectsFailClosed()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();

        AcceleratorGuardDecision decision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDeviceExecution(
                descriptor,
                guardEvidence: null);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AcceleratorGuardSurface.DeviceExecution, decision.Surface);
        Assert.Equal(AcceleratorGuardFault.MissingGuardEvidence, decision.Fault);
    }

    [Fact]
    public void CommitAfterOwnerDrift_RejectsFailClosedBeforePublication()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorOwnerBinding driftedOwner =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(ownerPodId: 99);
        AcceleratorGuardEvidence driftedEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                driftedOwner,
                activeDomainCertificate: descriptor.OwnerBinding.DomainTag);

        AcceleratorGuardDecision decision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeCommit(
                descriptor,
                driftedEvidence);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AcceleratorGuardSurface.Commit, decision.Surface);
        Assert.Equal(AcceleratorGuardFault.OwnerMismatch, decision.Fault);
        Assert.Equal(RejectKind.OwnerMismatch, decision.LegalityDecision.RejectKind);
    }

    [Theory]
    [InlineData(AcceleratorGuardEvidenceSource.RawVirtualThreadIdHint)]
    [InlineData(AcceleratorGuardEvidenceSource.TokenHandle)]
    [InlineData(AcceleratorGuardEvidenceSource.Telemetry)]
    [InlineData(AcceleratorGuardEvidenceSource.ReplayCertificateIdentity)]
    [InlineData(AcceleratorGuardEvidenceSource.RegistryMetadata)]
    public void EvidencePlaneObjects_CannotSatisfyOwnerDomainGuard(
        AcceleratorGuardEvidenceSource source)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence evidence = source == AcceleratorGuardEvidenceSource.RawVirtualThreadIdHint
            ? AcceleratorGuardEvidence.FromRawVirtualThreadIdHint(
                ownerBinding.OwnerVirtualThreadId,
                ownerBinding)
            : AcceleratorGuardEvidence.FromEvidencePlane(
                source,
                ownerBinding,
                evidenceIdentity: 0xACCE55,
                registryAcceleratorId: "matmul.fixture.v1");

        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                ownerBinding,
                evidence);
        AcceleratorDescriptorValidationResult result =
            AcceleratorDescriptorParser.Parse(
                descriptorBytes,
                guardDecision,
                L7SdcTestDescriptorFactory.CreateReference(descriptorBytes));

        Assert.False(guardDecision.IsAllowed);
        Assert.Equal(AcceleratorGuardFault.EvidenceSourceNotAuthority, guardDecision.Fault);
        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.OwnerDomainFault, result.Fault);
        if (source == AcceleratorGuardEvidenceSource.ReplayCertificateIdentity)
        {
            Assert.True(guardDecision.LegalityDecision.AttemptedReplayCertificateReuse);
        }
    }
}
