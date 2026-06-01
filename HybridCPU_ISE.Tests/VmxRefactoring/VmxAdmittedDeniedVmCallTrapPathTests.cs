using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmx;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxAdmittedDeniedVmCallTrapPathTests
{
    [Fact]
    public void VmCallTrapPath_AdmitsRuntimeBoundaryProjectsNeutralTrapAndDeniesBackendExecution()
    {
        var service = new VmxCompatibilityAdmissionService();
        VmxCompatibilityTrapAdmissionResult result = service.AdmitVmCallTrapProjection(
            CreateVmCallRequest());

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
            result.Decision);
        Assert.True(result.Decode.IsAllowed);
        Assert.Equal(VmxOperandForm.HypercallLeafAndDescriptor, result.Decode.Payload.OperandForm);
        Assert.True(result.Projection.IsAllowed);
        Assert.Equal(CompatAliasTargetKind.CompatibilityProjection, result.Projection.Alias.TargetKind);
        Assert.Equal("VmxTrapProjectionMapper.Project", result.Projection.Alias.TargetName);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.RuntimeAdmission.Decision);
        Assert.Equal(DomainRuntimeAuthorityDecision.Allowed, result.RuntimeAdmission.AuthorityResult.Decision);
        Assert.True(result.NeutralResult.ShouldTrap);
        Assert.Equal(
            NeutralTrapResultKind.CompatibilityOperationIntercept,
            result.NeutralResult.Kind);
        Assert.Equal(
            HypercallBackendAdmissionDecision.MissingBackendDescriptor,
            result.BackendAdmission.Decision);
        Assert.True(result.BackendAdmission.DeniesBackendExecution);
        Assert.Contains("no neutral runtime backend descriptor", result.BackendAdmission.Reason);
        Assert.True(result.ProjectedDecision.ShouldExit);
        Assert.Equal(VmExitReason.VmCall, result.ProjectedDecision.ExitReason);
        Assert.True(result.IsAdmittedDeniedTrapProjection);
        Assert.Contains("backend execution remains denied", result.Reason);
    }

    [Fact]
    public void VmCallTrapPath_DeniesAtRuntimeAdmissionWhenCompatibilityAliasEvidenceIsClosed()
    {
        var service = new VmxCompatibilityAdmissionService();
        VmxCompatibilityTrapAdmissionResult result = service.AdmitVmCallTrapProjection(
            CreateVmCallRequest(
                evidencePolicy: new EvidencePolicyDescriptor(
                    allowCompatibilityAliases: false,
                    allowGuestArchitecturalState: true,
                    allowMigrationSerializableState: false)));

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.RuntimeAdmissionDenied,
            result.Decision);
        Assert.True(result.Decode.IsAllowed);
        Assert.True(result.Projection.IsAllowed);
        Assert.False(result.RuntimeAdmissionAllowed);
        Assert.Equal(
            RuntimeBoundaryAdmissionDecision.EvidenceBoundaryDenied,
            result.RuntimeAdmission.Decision);
        Assert.Equal(
            NeutralTrapResultKind.SecurityPolicyViolation,
            result.NeutralResult.Kind);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, result.ProjectedDecision.ExitReason);
    }

    [Fact]
    public void VmCallTrapPath_DeniesBeforeRuntimeWhenProjectionEvidenceWasNotValidated()
    {
        var service = new VmxCompatibilityAdmissionService();
        VmxCompatibilityTrapAdmissionResult result = service.AdmitVmCallTrapProjection(
            CreateVmCallRequest(projectionEvidenceValidated: false));

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.ProjectionDenied,
            result.Decision);
        Assert.True(result.Decode.IsAllowed);
        Assert.False(result.Projection.IsAllowed);
        Assert.Equal(
            VmxCompatProjectionDecision.EvidenceValidationDenied,
            result.Projection.Decision);
        Assert.False(result.RuntimeAdmissionAllowed);
    }

    [Fact]
    public void VmCallTrapPath_DeniesAfterAdmissionWhenNeutralTrapPolicyDoesNotIntercept()
    {
        var service = new VmxCompatibilityAdmissionService();
        VmxCompatibilityTrapAdmissionResult result = service.AdmitVmCallTrapProjection(
            CreateVmCallRequest(trapBitmap: new TrapPolicyBitmap()));

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.TrapPolicyDenied,
            result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.Equal(
            NeutralTrapResultKind.SecurityPolicyViolation,
            result.NeutralResult.Kind);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, result.ProjectedDecision.ExitReason);
        Assert.Contains("neutral compatibility-operation intercept", result.Reason);
    }

    [Fact]
    public void VmCallTrapPath_SourceUsesRuntimeAdmissionNeutralTrapAndMapperWithoutBackendMarkers()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs");
        string dispatcherAndRetireSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs",
            "CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");

        Assert.Contains("RuntimeBoundaryAdmissionService", source);
        Assert.Contains("DomainRuntimeOperationKind.ProjectCompatibilityTrap", source);
        Assert.Contains("NeutralTrapResult", source);
        Assert.Contains("HypercallBackendAdmissionService.Default.Admit", source);
        Assert.Contains("HypercallBackendAdmissionRequest.MissingNeutralOwner", source);
        Assert.Contains("HypercallBackendAdmissionResult BackendAdmission", source);
        Assert.Contains("BackendAdmission.DeniesBackendExecution", source);
        Assert.Contains("VmxTrapProjectionMapper.Default.Project", source);
        Assert.Contains("\"VMCALL\"", source);
        Assert.DoesNotContain("VmExitReason.", source);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", source);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", source);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", source);
        Assert.DoesNotContain("VmxRetireEffect.VmFunc", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);

        Assert.Contains("VmxRetireEffect.Fault", dispatcherAndRetireSource);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", dispatcherAndRetireSource);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", dispatcherAndRetireSource);
        Assert.DoesNotContain("VmxRetireEffect.VmFunc", dispatcherAndRetireSource);
    }

    private static VmxCompatibilityVmCallTrapAdmissionRequest CreateVmCallRequest(
        EvidencePolicyDescriptor? evidencePolicy = null,
        TrapPolicyBitmap? trapBitmap = null,
        bool projectionEvidenceValidated = true) =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: evidencePolicy ?? CreateAliasEvidencePolicy(),
            TrapPolicy: new TrapPolicyDescriptor().WithEnabledClasses(
                TrapPolicyClass.CompatibilityOperation),
            TrapBitmap: trapBitmap ?? CreateVmCallTrapBitmap(),
            VtId: 1,
            HypercallLeafRegister: 2,
            DescriptorRegister: 3,
            ExecutionDomainTag: 4,
            AddressSpaceTag: 5,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: projectionEvidenceValidated,
            DomainValidated: true);

    private static TrapPolicyBitmap CreateVmCallTrapBitmap()
    {
        var bitmap = new TrapPolicyBitmap();
        bitmap.EnableVmxOperation(VmxOperationKind.VmCall);
        return bitmap;
    }

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
}
