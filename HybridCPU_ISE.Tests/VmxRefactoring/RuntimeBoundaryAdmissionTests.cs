// Description: Conformance tests for the neutral runtime boundary admission service.
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests;

public sealed class RuntimeBoundaryAdmissionTests
{
    [Fact]
    public void RuntimeBoundaryAdmission_AllowsRuntimeOwnedDomainEnterWithTypedGrantAndEvidencePolicy()
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;
        var service = new RuntimeBoundaryAdmissionService();

        RuntimeBoundaryAdmissionResult result = service.Validate(new RuntimeBoundaryAdmissionRequest(
            Context: CreateContext(capability),
            RootAuthority: CreateRoot(capability),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            Operation: new DomainRuntimeOperation(
                DomainRuntimeOperationKind.EnterDomain,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: true,
                isProjectionOnly: false),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                capability,
                CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.GuestArchitecturalState)));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_DeniesCompatibilityFrontendAuthoritativeMutation()
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;
        var service = new RuntimeBoundaryAdmissionService();

        RuntimeBoundaryAdmissionResult result = service.Validate(new RuntimeBoundaryAdmissionRequest(
            Context: CreateContext(capability),
            RootAuthority: CreateRoot(capability),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            Operation: new DomainRuntimeOperation(
                DomainRuntimeOperationKind.EnterDomain,
                DomainRuntimeOperationSource.CompatibilityFrontend,
                requiresCapabilityGrant: true,
                isProjectionOnly: false),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                capability,
                CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.GuestArchitecturalState)));

        Assert.False(result.IsAllowed);
        Assert.Equal(
            RuntimeBoundaryAdmissionDecision.FrontendAuthoritativeMutationDenied,
            result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_DeniesMissingTypedCapabilityGrant()
    {
        const ulong grantedCapability = VmxV2InstructionCaps.VmFunc;
        const ulong missingCapability = VmxV2InstructionCaps.VmRestX;
        var service = new RuntimeBoundaryAdmissionService();

        RuntimeBoundaryAdmissionResult result = service.Validate(new RuntimeBoundaryAdmissionRequest(
            Context: CreateContext(grantedCapability),
            RootAuthority: CreateRoot(grantedCapability | missingCapability),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            Operation: new DomainRuntimeOperation(
                DomainRuntimeOperationKind.EnterDomain,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: true,
                isProjectionOnly: false),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                missingCapability,
                CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.GuestArchitecturalState)));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.CapabilityBoundaryDenied, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_DeniesHostOwnedEvidenceExposure()
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;
        var service = new RuntimeBoundaryAdmissionService();

        RuntimeBoundaryAdmissionResult result = service.Validate(new RuntimeBoundaryAdmissionRequest(
            Context: CreateContext(capability),
            RootAuthority: CreateRoot(capability),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: true),
            Operation: new DomainRuntimeOperation(
                DomainRuntimeOperationKind.ReadCompatibilityProjection,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: true,
                isProjectionOnly: true),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                capability,
                CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.HostOwnedRuntimeEvidence)));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.EvidenceBoundaryDenied, result.Decision);
    }

    private static DomainRuntimeContext CreateContext(ulong capabilities) =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: capabilities,
                runtimeEnabledCaps: capabilities,
                domainGrantedCaps: capabilities));

    private static RootAuthorityDescriptor CreateRoot(ulong capabilities) =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: capabilities,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: true);
}
