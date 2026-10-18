using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ExternalSecureComputeContractTests
{
    [Fact]
    public void SecureBindings_AreImmutableAndComposeExactExistingLeaseLineage()
    {
        ExternalDomainLease parent = new(new ExternalDomainLeaseHandle(Guid.NewGuid()), new ExternalDomainLeaseEpoch(1));
        ExternalChildDomainLease child = new(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), parent);
        ExternalGuestMappingLease guest = new(new ExternalGuestMappingHandle(Guid.NewGuid()), new ExternalGuestMappingEpoch(1), child);
        ExternalSecureDomainLease secure = new(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1));
        var execution = new ExternalSecureExecutionBinding(child, secure, parent, 1, ExternalSecureComputeContract.Version);
        var region = new ExternalSecureRegionBinding(
            secure, guest, new ExternalSecureRegionHandle(Guid.NewGuid()),
            new ExternalSecureRegionGeneration(1), ExternalSecureComputeContract.Version);
        var guestRegion = new ExternalSecureGuestRegionBinding(execution, region);

        Assert.Equal(child, execution.Child);
        Assert.Equal(secure, execution.SecureDomain);
        Assert.Equal(guest, region.ParentLease);
        Assert.Equal(region, guestRegion.Region);
        Assert.All(typeof(ExternalSecureExecutionBinding).GetProperties(), property => Assert.False(property.CanWrite));
        Assert.All(typeof(ExternalSecureRegionBinding).GetProperties(), property => Assert.False(property.CanWrite));
    }

    [Fact]
    public void SecureBindingsAndClosure_FailClosedForVersionOrLineageMismatch()
    {
        ExternalDomainLease parent = new(new ExternalDomainLeaseHandle(Guid.NewGuid()), new ExternalDomainLeaseEpoch(1));
        ExternalChildDomainLease child = new(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), parent);
        ExternalGuestMappingLease guest = new(new ExternalGuestMappingHandle(Guid.NewGuid()), new ExternalGuestMappingEpoch(1), child);
        ExternalSecureDomainLease secure = new(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1));
        var region = new ExternalSecureRegionBinding(
            secure, guest, new ExternalSecureRegionHandle(Guid.NewGuid()),
            new ExternalSecureRegionGeneration(1), ExternalSecureComputeContract.Version);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalSecureExecutionBinding(
            child, secure, parent, 1, new HybridCpuExternalContractVersion(1, 0, 1)));
        Assert.Throws<ArgumentException>(() => new ExternalSecureRegionCloseReceipt(
            region, new ExternalOperationIdentity(new ExternalOperationHandle(Guid.NewGuid()), new ExternalOperationGeneration(1)), false));
        Assert.Throws<ArgumentException>(() => new ExternalSecureGuestRegionBinding(
            new ExternalSecureExecutionBinding(child, secure, parent, 1, ExternalSecureComputeContract.Version),
            new ExternalSecureRegionBinding(
                new ExternalSecureDomainLease(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1)),
                guest, new ExternalSecureRegionHandle(Guid.NewGuid()), new ExternalSecureRegionGeneration(1),
                ExternalSecureComputeContract.Version)));
    }

    [Fact]
    public void SecureDomainCreation_RequiresExactProviderProofAndProperties()
    {
        ExternalDomainLease parent = new(new ExternalDomainLeaseHandle(Guid.NewGuid()), new ExternalDomainLeaseEpoch(1));
        var properties = new ExternalSecureDomainProperties(ExternalSecureAssuranceClass.High, true, true);
        var request = new ExternalSecureDomainCreateRequest(
            parent, new ExternalSecureEvidenceContextHandle(Guid.NewGuid()), properties, ExternalSecureComputeContract.Version);
        ExternalSecureDomainLease domain = new(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1));

        var receipt = new ExternalSecureDomainCreateReceipt(
            request, domain, properties, new ExternalSecureDomainCreationProof(Guid.NewGuid()));
        Assert.Equal(domain, receipt.Domain);
        Assert.Throws<ArgumentException>(() => new ExternalSecureDomainCreateReceipt(
            request, domain,
            new ExternalSecureDomainProperties(ExternalSecureAssuranceClass.Basic, true, true),
            new ExternalSecureDomainCreationProof(Guid.NewGuid())));
        Assert.Throws<ArgumentException>(() => new ExternalSecureDomainCreateReceipt(
            request, domain, properties, default));
    }

    [Fact]
    public void SecureVirtualIo_RequiresExistingExactChildReceiptAndSecureAdmissionProof()
    {
        ExternalDomainLease parent = new(new ExternalDomainLeaseHandle(Guid.NewGuid()), new ExternalDomainLeaseEpoch(1));
        ExternalChildDomainLease child = new(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), parent);
        ExternalSecureDomainLease secure = new(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1));
        var execution = new ExternalSecureExecutionBinding(child, secure, parent, 1, ExternalSecureComputeContract.Version);
        ExternalOperationIdentity operation = new(new ExternalOperationHandle(Guid.NewGuid()), new ExternalOperationGeneration(1));
        var virtualIo = new ExternalChildVirtualIoBindReceipt(
            new ExternalChildVirtualIoHandle(Guid.NewGuid()), new ExternalChildVirtualIoEpoch(1),
            child.Handle, child.Epoch, parent, new ExternalParentDeviceHandle(Guid.NewGuid()),
            new ExternalParentDeviceEpoch(1), ExternalChildVirtualIoRights.Read, 4096,
            operation, new HybridCpuExternalContractVersion(1, 0, 0), 1);

        var binding = new ExternalSecureVirtualIoBinding(execution, virtualIo);
        var admission = new ExternalSecureIoAdmissionReceipt(
            binding, operation, new ExternalSecureIoAdmissionProof(Guid.NewGuid()));
        Assert.Equal(binding, admission.Binding);
        Assert.Throws<ArgumentException>(() => new ExternalSecureIoAdmissionReceipt(binding, operation, default));
    }
}
