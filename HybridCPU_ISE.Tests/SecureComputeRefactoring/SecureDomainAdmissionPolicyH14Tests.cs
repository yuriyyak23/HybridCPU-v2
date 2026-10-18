using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureDomainAdmissionPolicyH14Tests
{
    [Theory]
    [InlineData(SecureDomainOperationClass.CreateEvidence, SecureDomainAdmissionDecision.DeniedMissingEvidencePolicy)]
    [InlineData(SecureDomainOperationClass.PublishCompletion, SecureDomainAdmissionDecision.DeniedMissingEvidencePolicy)]
    [InlineData(SecureDomainOperationClass.PublishRetireSideEffect, SecureDomainAdmissionDecision.DeniedMissingEvidencePolicy)]
    [InlineData(SecureDomainOperationClass.SecureMigration, SecureDomainAdmissionDecision.DeniedMissingMigrationPolicy)]
    [InlineData(SecureDomainOperationClass.NestedSecureDomain, SecureDomainAdmissionDecision.DeniedUnsupportedOperationClass)]
    [InlineData(SecureDomainOperationClass.CompatibilityProjection, SecureDomainAdmissionDecision.DeniedMissingEvidencePolicy)]
    public void ExplicitSecureOperationClasses_FailClosedWithoutTheirRequiredPolicy(
        SecureDomainOperationClass operationClass,
        SecureDomainAdmissionDecision expected)
    {
        SecureDomainAdmissionResult result = new SecureDomainAdmissionPolicy().Admit(
            MaterializedFailClosedDescriptor(), operationClass, measurement: null, memory: null);

        Assert.False(result.IsAllowed);
        Assert.Equal(expected, result.Decision);
    }

    [Fact]
    public void UndefinedSecureOperationClass_DeniesByDefault()
    {
        SecureDomainAdmissionResult result = new SecureDomainAdmissionPolicy().Admit(
            MaterializedFailClosedDescriptor(), (SecureDomainOperationClass)255, measurement: null, memory: null);

        Assert.False(result.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.DeniedUnsupportedOperationClass, result.Decision);
    }

    private static SecureComputeDomainDescriptor MaterializedFailClosedDescriptor() => new(
        domainTag: 1,
        securityLevel: SecureComputeSecurityLevel.Measured,
        measurementRequired: false,
        privateMemoryRequired: false,
        hostInspectionPolicy: SecureHostInspectionPolicy.DenyAll,
        evidenceVisibilityPolicy: SecureEvidencePolicy.FailClosed,
        migrationPolicy: SecureMigrationDescriptor.Disabled,
        ioPolicy: SecureIoDomainDescriptor.Disabled,
        hypercallPolicy: SecureHypercallDescriptor.Disabled,
        debugPolicy: SecureDebugPolicy.Denied,
        compatibilityProjectionPolicy: SecureCompatibilityProjectionPolicy.DenyAll);
}
