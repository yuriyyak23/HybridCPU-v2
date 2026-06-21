using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcMappingEpochGuardTests
{
    [Fact]
    public void MappingEpochDrift_RejectsFailClosedAtCommitBoundary()
    {
        AcceleratorCommandDescriptor descriptor = ParseDescriptorWithEpochs(
            mappingEpoch: 10,
            iommuDomainEpoch: 20);
        AcceleratorGuardEvidence driftedEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                descriptor.OwnerBinding,
                activeDomainCertificate: descriptor.OwnerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(11),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(20));

        AcceleratorGuardDecision decision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeCommit(
                descriptor,
                driftedEvidence);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AcceleratorGuardFault.MappingEpochDrift, decision.Fault);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.RejectKind.EpochMismatch, decision.LegalityDecision.RejectKind);
    }

    [Fact]
    public void IommuDomainEpochDrift_RejectsFailClosedAtCommitBoundary()
    {
        AcceleratorCommandDescriptor descriptor = ParseDescriptorWithEpochs(
            mappingEpoch: 10,
            iommuDomainEpoch: 20);
        AcceleratorGuardEvidence driftedEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                descriptor.OwnerBinding,
                activeDomainCertificate: descriptor.OwnerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(10),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(21));

        AcceleratorGuardDecision decision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeCommit(
                descriptor,
                driftedEvidence);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AcceleratorGuardFault.IommuDomainEpochDrift, decision.Fault);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.RejectKind.EpochMismatch, decision.LegalityDecision.RejectKind);
    }

    [Fact]
    public void StableMappingAndIommuEpochs_AllowModelGuardWithoutCreatingCommitPath()
    {
        AcceleratorCommandDescriptor descriptor = ParseDescriptorWithEpochs(
            mappingEpoch: 10,
            iommuDomainEpoch: 20);
        AcceleratorGuardEvidence currentEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                descriptor.OwnerBinding,
                activeDomainCertificate: descriptor.OwnerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(10),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(20));

        AcceleratorGuardDecision decision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                descriptor.OwnerGuardDecision,
                currentEvidence);

        Assert.True(decision.IsAllowed, decision.Message);
        Assert.Equal(AcceleratorGuardSurface.MappingEpochValidation, decision.Surface);
        Assert.Equal(new AcceleratorMappingEpoch(10), decision.MappingEpoch);
        Assert.Equal(new AcceleratorIommuDomainEpoch(20), decision.IommuDomainEpoch);
    }

    [Fact]
    public void MappingEpochEvidence_MustComeFromGuardPlane()
    {
        AcceleratorCommandDescriptor descriptor = ParseDescriptorWithEpochs(
            mappingEpoch: 10,
            iommuDomainEpoch: 20);
        AcceleratorGuardEvidence tokenEvidence =
            AcceleratorGuardEvidence.FromEvidencePlane(
                AcceleratorGuardEvidenceSource.TokenHandle,
                descriptor.OwnerBinding,
                evidenceIdentity: 0x1234);

        AcceleratorGuardDecision decision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                descriptor.OwnerGuardDecision,
                tokenEvidence);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AcceleratorGuardFault.EvidenceSourceNotAuthority, decision.Fault);
    }

    private static AcceleratorCommandDescriptor ParseDescriptorWithEpochs(
        ulong mappingEpoch,
        ulong iommuDomainEpoch)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence evidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                ownerBinding,
                activeDomainCertificate: ownerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(mappingEpoch),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(iommuDomainEpoch));
        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes, evidence);

        Assert.True(result.IsValid, result.Message);
        return result.RequireDescriptor();
    }
}
