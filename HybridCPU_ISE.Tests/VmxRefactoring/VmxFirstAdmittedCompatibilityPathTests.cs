using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmx;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests;

public sealed class VmxFirstAdmittedCompatibilityPathTests
{
    [Fact]
    public void VmReadProjectionPath_AdmitsRuntimeBoundaryThenStopsAtDeniedScalarProjectionAbi()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                descriptor: VmcsV2Descriptor.CreateDefault(),
                evidencePolicy: CreateAliasEvidencePolicy()));

        Assert.Equal(
            VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied,
            result.Decision);
        Assert.True(result.Decode.IsAllowed);
        Assert.Equal(VmxOperandForm.FieldSelectorToRegister, result.Decode.Payload.OperandForm);
        Assert.True(result.Projection.IsAllowed);
        Assert.Equal(CompatAliasTargetKind.CompatibilityProjection, result.Projection.Alias.TargetKind);
        Assert.Equal("VmcsFieldAliasProjection.Read", result.Projection.Alias.TargetName);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.RuntimeAdmission.Decision);
        Assert.Equal(DomainRuntimeAuthorityDecision.Allowed, result.RuntimeAdmission.AuthorityResult.Decision);
        Assert.True(result.IsReadOnlyProjectionDenied);
        Assert.False(result.IsScalarProjectionAllowed);
        Assert.Equal(0, result.Value);
        Assert.Equal(VmcsV2ValidationCode.AccessDenied, result.VmcsValidation.Code);
        Assert.Contains("scalar VMREAD requires generated read-only projection", result.Reason);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesAtRuntimeAdmissionWhenAliasEvidencePolicyIsClosed()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                descriptor: VmcsV2Descriptor.CreateDefault(),
                evidencePolicy: new EvidencePolicyDescriptor(
                    allowCompatibilityAliases: false,
                    allowGuestArchitecturalState: true,
                    allowMigrationSerializableState: false)));

        Assert.Equal(
            VmxCompatibilityVmReadAdmissionDecision.RuntimeAdmissionDenied,
            result.Decision);
        Assert.True(result.Decode.IsAllowed);
        Assert.True(result.Projection.IsAllowed);
        Assert.False(result.RuntimeAdmissionAllowed);
        Assert.Equal(
            RuntimeBoundaryAdmissionDecision.EvidenceBoundaryDenied,
            result.RuntimeAdmission.Decision);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void VmReadProjectionPath_DeniesBeforeRuntimeWhenProjectionEvidenceWasNotValidated()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityVmReadAdmissionResult result = service.AdmitVmReadProjection(
            CreateVmReadRequest(
                descriptor: VmcsV2Descriptor.CreateDefault(),
                evidencePolicy: CreateAliasEvidencePolicy(),
                projectionEvidenceValidated: false));

        Assert.Equal(
            VmxCompatibilityVmReadAdmissionDecision.ProjectionDenied,
            result.Decision);
        Assert.True(result.Decode.IsAllowed);
        Assert.False(result.Projection.IsAllowed);
        Assert.Equal(
            VmxCompatProjectionDecision.EvidenceValidationDenied,
            result.Projection.Decision);
        Assert.False(result.RuntimeAdmissionAllowed);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void VmReadProjectionPath_SourceUsesRuntimeAdmissionWithoutBackendAuthorityMarkers()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string source = File.ReadAllText(Path.Combine(
            projectRoot,
            "Core",
            "VMX",
            "Compatibility",
            "Frontend",
            "Handlers",
            "VmxCompatibilityAdmissionService.cs"));

        Assert.Contains("RuntimeBoundaryAdmissionService", source);
        Assert.Contains("DomainRuntimeOperationKind.ReadCompatibilityProjection", source);
        Assert.Contains("EvidenceVisibilityClass.CompatibilityAlias", source);
        Assert.Contains("TryReadScalarField", source);
        Assert.DoesNotContain("VmxRetireEffect.VmcsRead", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
    }

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsV2Descriptor? descriptor,
        EvidencePolicyDescriptor evidencePolicy,
        bool projectionEvidenceValidated = true) =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: evidencePolicy,
            Descriptor: descriptor,
            FieldId: (ushort)VmcsField.GuestPc,
            DestinationRegister: 3,
            FieldSelectorRegister: 1,
            ReservedRegister: 0,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: projectionEvidenceValidated);

    private static DomainRuntimeContext CreateContext() =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: 0,
                runtimeEnabledCaps: 0,
                domainGrantedCaps: 0));

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: false);

    private static EvidencePolicyDescriptor CreateAliasEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
