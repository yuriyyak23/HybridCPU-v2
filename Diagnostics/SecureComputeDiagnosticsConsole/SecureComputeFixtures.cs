using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal static class SecureComputeFixtures
{
    private const ulong Capability = VmxV2InstructionCaps.VmFunc;

    public static RuntimeBoundaryAdmissionRequest CreateRequest(
        DomainRuntimeContext context,
        SecureComputeDomainDescriptor? requestDescriptor,
        SecureDomainOperationClass operationClass) =>
        new(
            Context: context,
            RootAuthority: new RootAuthorityDescriptor(
                RootAuthorityClass.RuntimeRoot, 1, Capability, true, true),
            EvidencePolicy: new EvidencePolicyDescriptor(false, true, false),
            Operation: new DomainRuntimeOperation(
                DomainRuntimeOperationKind.EnterDomain,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: true,
                isProjectionOnly: false),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                Capability, CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.GuestArchitecturalState),
            SecureDescriptor: requestDescriptor,
            SecureOperationClass: operationClass,
            SecureMeasurement: null,
            SecureMemory: null);

    public static DomainRuntimeContext CreateContext(
        SecureComputeDomainDescriptor? descriptor,
        ulong? domainTag = null) =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(Capability, Capability, Capability),
            secureCompute: descriptor,
            domainTag: domainTag ?? descriptor?.DomainTag ?? 0,
            addressSpaceTag: 9);

    public static SecureComputeDomainDescriptor CreateDescriptor(
        ulong domainTag,
        bool measurementRequired = false,
        bool privateMemoryRequired = false) =>
        new(
            domainTag,
            SecureComputeSecurityLevel.Measured,
            measurementRequired,
            privateMemoryRequired,
            SecureHostInspectionPolicy.DenyAll,
            SecureEvidencePolicy.FailClosed,
            SecureMigrationDescriptor.Disabled,
            SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            SecureDebugPolicy.Denied,
            SecureCompatibilityProjectionPolicy.DenyAll);
}
