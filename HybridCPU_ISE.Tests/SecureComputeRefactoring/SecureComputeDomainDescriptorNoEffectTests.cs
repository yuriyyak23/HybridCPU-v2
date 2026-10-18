using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureComputeDomainDescriptorNoEffectTests
{
    [Fact]
    public void AbsentDescriptor_AllowsOrdinaryOperationAsNoEffect()
    {
        SecureDomainAdmissionResult result = new SecureDomainAdmissionService().Admit(
            descriptor: null,
            operationClass: SecureDomainOperationClass.Ordinary,
            measurement: null,
            memory: null);

        Assert.True(result.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.AllowedNoEffect, result.Decision);
    }

    [Fact]
    public void DisabledDescriptor_AllowsOrdinaryOperationAsNoEffect()
    {
        SecureComputeDomainDescriptor descriptor = SecureComputeDomainDescriptor.Disabled;

        SecureDomainAdmissionResult result = new SecureDomainAdmissionService().Admit(
            descriptor,
            SecureDomainOperationClass.Ordinary,
            measurement: null,
            memory: null);

        Assert.False(descriptor.IsEnabled);
        Assert.False(descriptor.IsMaterialized);
        Assert.False(descriptor.IsActive);
        Assert.True(descriptor.IsNoEffect);
        Assert.True(result.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.AllowedNoEffect, result.Decision);
    }

    [Fact]
    public void SecurityLevelNone_NormalizesToDisabledNoEffectState()
    {
        SecureComputeDomainDescriptor descriptor = CreateDescriptor(
            domainTag: 7,
            securityLevel: SecureComputeSecurityLevel.None,
            measurementRequired: true,
            privateMemoryRequired: true);

        SecureDomainAdmissionResult result = new SecureDomainAdmissionService().Admit(
            descriptor,
            SecureDomainOperationClass.Ordinary,
            measurement: null,
            memory: null);

        Assert.Equal(SecureComputeSecurityLevel.Disabled, descriptor.SecurityLevel);
        Assert.False(descriptor.IsEnabled);
        Assert.True(descriptor.IsMaterialized);
        Assert.False(descriptor.IsActive);
        Assert.True(descriptor.IsNoEffect);
        Assert.False(descriptor.RequiresMeasurement);
        Assert.False(descriptor.RequiresSecureMemoryPolicy);
        Assert.True(result.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.AllowedNoEffect, result.Decision);
    }

    [Fact]
    public void EnabledDescriptorWithoutMaterialization_IsNotActive()
    {
        SecureComputeDomainDescriptor descriptor = CreateDescriptor(
            domainTag: 0,
            securityLevel: SecureComputeSecurityLevel.Measured,
            measurementRequired: true,
            privateMemoryRequired: false);

        SecureDomainAdmissionResult result = new SecureDomainAdmissionService().Admit(
            descriptor,
            SecureDomainOperationClass.EnterSecureDomain,
            measurement: null,
            memory: null);

        Assert.True(descriptor.IsEnabled);
        Assert.False(descriptor.IsMaterialized);
        Assert.False(descriptor.IsActive);
        Assert.True(descriptor.IsNoEffect);
        Assert.False(result.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.DeniedUnmaterializedDescriptor, result.Decision);
    }

    [Fact]
    public void EnabledDescriptor_DoesNotOverDenyOrdinaryOperationClass()
    {
        SecureComputeDomainDescriptor descriptor = CreateDescriptor(
            domainTag: 11,
            securityLevel: SecureComputeSecurityLevel.Private,
            measurementRequired: true,
            privateMemoryRequired: true);

        SecureDomainAdmissionResult result = new SecureDomainAdmissionService().Admit(
            descriptor,
            SecureDomainOperationClass.Ordinary,
            measurement: null,
            memory: null);

        Assert.True(descriptor.IsActive);
        Assert.True(result.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.AllowedNoEffect, result.Decision);
    }

    [Fact]
    public void EnabledDescriptor_FailsClosedOnlyForSecureOperationMissingRequiredSubpolicy()
    {
        SecureComputeDomainDescriptor descriptor = CreateDescriptor(
            domainTag: 19,
            securityLevel: SecureComputeSecurityLevel.Measured,
            measurementRequired: true,
            privateMemoryRequired: false);

        SecureDomainAdmissionResult result = new SecureDomainAdmissionService().Admit(
            descriptor,
            SecureDomainOperationClass.EnterSecureDomain,
            measurement: null,
            memory: null);

        Assert.True(descriptor.IsActive);
        Assert.False(result.IsAllowed);
        Assert.Equal(SecureDomainAdmissionDecision.DeniedMissingMeasurement, result.Decision);
    }

    [Fact]
    public void NoEmissionGuard_DeniesForbiddenIsaAndMemoryShapes()
    {
        var contract = new SecureComputeNoEmissionContract();

        Assert.Equal(
            SecureComputeNoEmissionViolation.NewInstructionEncoding,
            contract.Validate(
                emitsNewInstructionEncoding: true,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: false,
                emitsVmxSecureMode: false));
        Assert.Equal(
            SecureComputeNoEmissionViolation.NewOperandFormat,
            contract.Validate(
                emitsNewInstructionEncoding: false,
                emitsNewOperandFormat: true,
                emitsCapabilityAwareLoadStoreFetch: false,
                emitsVmxSecureMode: false));
        Assert.Equal(
            SecureComputeNoEmissionViolation.CapabilityAwareLoadStoreFetch,
            contract.Validate(
                emitsNewInstructionEncoding: false,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: true,
                emitsVmxSecureMode: false));
        Assert.Equal(
            SecureComputeNoEmissionViolation.VmxSecureModeEmission,
            contract.Validate(
                emitsNewInstructionEncoding: false,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: false,
                emitsVmxSecureMode: true));
        Assert.Equal(
            SecureComputeNoEmissionViolation.None,
            contract.Validate(
                emitsNewInstructionEncoding: false,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: false,
                emitsVmxSecureMode: false));
    }

    private static SecureComputeDomainDescriptor CreateDescriptor(
        ulong domainTag,
        SecureComputeSecurityLevel securityLevel,
        bool measurementRequired,
        bool privateMemoryRequired) =>
        new(
            domainTag,
            securityLevel,
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
