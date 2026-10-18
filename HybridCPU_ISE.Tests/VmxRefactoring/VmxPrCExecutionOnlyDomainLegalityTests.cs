using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrCExecutionOnlyDomainLegalityTests
{
    [Fact]
    public void ExecutionOnly_NoStateRuntimeOperation_DoesNotRequireMemoryIoOrMutationPrivilege()
    {
        DomainRuntimeContext context = CreateExecutionOnlyContext();
        DomainRuntimeOperation operation = CreateNoStateOperation();
        CapabilityBoundaryRequirement capability = CreateCapabilityRequirement();

        DomainValidationResult legality = new DomainLegalityService().Validate(
            context,
            operation,
            DomainBoundaryDescriptor.ExecutionOnly,
            capability);
        RuntimeBoundaryAdmissionResult admission = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(context, operation, capability));

        Assert.True(legality.IsValid);
        Assert.True(admission.IsAllowed);
        Assert.Equal(DomainRuntimeAuthorityDecision.Allowed, admission.AuthorityResult.Decision);
        Assert.False(operation.CanMutateAuthoritativeState);
        Assert.False(operation.IsProjectionOnly);
    }

    [Fact]
    public void CompatibilityFrontend_CannotClaimNoStateExecutionAuthority()
    {
        DomainRuntimeOperation operation = new(
            DomainRuntimeOperationKind.InvokeCapability,
            DomainRuntimeOperationSource.CompatibilityFrontend,
            requiresCapabilityGrant: true,
            DomainRuntimeOperationAuthorityClass.NoStateExecution);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(CreateExecutionOnlyContext(), operation, CreateCapabilityRequirement()));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.RuntimeAuthorityDenied, result.Decision);
    }

    [Fact]
    public void ExecutionOnly_StillRequiresExecutionDomainAndExactTypedGrant()
    {
        DomainRuntimeOperation operation = CreateNoStateOperation();
        DomainRuntimeContext noExecution = new(
            execution: null,
            memory: null,
            io: null,
            capabilities: CreateCapabilities(),
            secureCompute: null,
            domainTag: 7,
            addressSpaceTag: 0);
        DomainRuntimeContext noGrant = new(
            execution: new ExecutionDomainDescriptor(
                domainTag: 7,
                new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
                null,
                null,
                false),
            memory: null,
            io: null,
            capabilities: CapabilityDescriptorSet.Empty,
            secureCompute: null,
            domainTag: 7,
            addressSpaceTag: 0);

        RuntimeBoundaryAdmissionResult missingExecution =
            new RuntimeBoundaryAdmissionService().Validate(
                CreateRequest(noExecution, operation, CreateCapabilityRequirement()));
        RuntimeBoundaryAdmissionResult missingGrant =
            new RuntimeBoundaryAdmissionService().Validate(
                CreateRequest(noGrant, operation, CreateCapabilityRequirement()));

        Assert.Equal(RuntimeBoundaryAdmissionDecision.DomainBoundaryDenied, missingExecution.Decision);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.CapabilityBoundaryDenied, missingGrant.Decision);
    }

    [Fact]
    public void ExecutionOnly_IsDeniedInsideSecureComputeDomain()
    {
        SecureComputeDomainDescriptor secure = new(
            domainTag: 7,
            SecureComputeSecurityLevel.Measured,
            measurementRequired: false,
            privateMemoryRequired: false,
            SecureHostInspectionPolicy.DenyAll,
            SecureEvidencePolicy.FailClosed,
            SecureMigrationDescriptor.Disabled,
            SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            SecureDebugPolicy.Denied,
            SecureCompatibilityProjectionPolicy.DenyAll);
        DomainRuntimeContext context = CreateExecutionOnlyContext(secure);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(context, CreateNoStateOperation(), CreateCapabilityRequirement()));

        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
    }

    [Fact]
    public void LegacyOverloads_RemainFullDomainRuntimeAndMutationGated()
    {
        DomainRuntimeContext context = CreateExecutionOnlyContext();
        DomainRuntimeOperation operation = new(
            DomainRuntimeOperationKind.EnterDomain,
            DomainRuntimeOperationSource.RuntimeService,
            requiresCapabilityGrant: false,
            isProjectionOnly: false);

        DomainValidationResult legality = new DomainLegalityService().Validate(context, operation);
        DomainRuntimeAuthorityResult authority = new DomainRuntimeAuthority().Validate(
            CreateRoot(),
            context,
            operation);

        Assert.Equal(DomainValidationFailureReason.MissingMemoryDomain, legality.FailureReason);
        Assert.Equal(DomainRuntimeAuthorityDecision.MissingRuntimeContext, authority.Decision);
    }

    private static RuntimeBoundaryAdmissionRequest CreateRequest(
        DomainRuntimeContext context,
        DomainRuntimeOperation operation,
        CapabilityBoundaryRequirement capability) =>
        new(
            context,
            CreateRoot(),
            EvidencePolicy: null,
            operation,
            DomainBoundaryDescriptor.ExecutionOnly,
            capability,
            EvidenceBoundaryRequirement.None);

    private static DomainRuntimeOperation CreateNoStateOperation() =>
        new(
            DomainRuntimeOperationKind.InvokeCapability,
            DomainRuntimeOperationSource.RuntimeService,
            requiresCapabilityGrant: true,
            DomainRuntimeOperationAuthorityClass.NoStateExecution);

    private static DomainRuntimeContext CreateExecutionOnlyContext(
        SecureComputeDomainDescriptor? secure = null) =>
        new(
            execution: new ExecutionDomainDescriptor(
                domainTag: 7,
                bundleLegality: new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
                schedulingBudget: null,
                extension: null,
                compatibilityProjectionEnabled: false),
            memory: null,
            io: null,
            capabilities: CreateCapabilities(),
            secureCompute: secure,
            domainTag: 7,
            addressSpaceTag: 0);

    private static CapabilityDescriptorSet CreateCapabilities() =>
        new(new CapabilityGrantCollection([
            new CapabilityGrant(
                RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                CapabilityGrantScope.DomainGranted,
                isGranted: true,
                ownerDomainId: 7,
                CapabilityDelegationPolicy.NonDelegable,
                CapabilityRevocationPolicy.RuntimeRevocable,
                CapabilityMigrationClass.DomainLocal,
                CapabilityEvidenceVisibility.HostOnly,
                CapabilityFrontendProjectionPolicy.NeverProject),
        ]));

    private static CapabilityBoundaryRequirement CreateCapabilityRequirement() =>
        CapabilityBoundaryRequirement.TypedGrant(
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            CapabilityGrantScope.DomainGranted);

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            allowCompatibilityFrontendActivation: false,
            allowAuthoritativeStateMutation: false);
}
