using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class VmxTrapCompletionRouteOwnerTests
{
    [Fact]
    public void ProjectionOnlyTrapCompletionRoute_DeniesPublicationBeforeBackendExecution()
    {
        TrapRequest request = CreateTrapRequest();
        NeutralTrapResult trap = NeutralTrapResult.Trap(
            request,
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        TrapCompletionRouteRequest routeRequest =
            TrapCompletionRouteRequest.ProjectionOnlyDenied(
                trap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed));

        TrapCompletionRouteResult route =
            TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult fence =
            TrapCompletionRouteService.Default.EvaluateFence(
                routeRequest,
                route);

        Assert.Equal(
            TrapCompletionRouteDecision.DeniedBackendExecution,
            route.Decision);
        Assert.True(route.DeniesBackendExecution);
        Assert.False(route.CompletionPublicationAuthorized);
        Assert.False(route.RetirePublicationAuthorized);
        Assert.Equal(
            TrapCompletionPublicationDecision.DeniedBackendExecution,
            fence.Decision);
        Assert.False(fence.CompletionPublicationAllowed);
        Assert.False(fence.RetirePublicationAllowed);
        Assert.True(fence.Completion.IsEmpty);
    }

    [Fact]
    public void RuntimeOwnedTrapCompletionRoute_CanAuthorizeNeutralTrapOnlyAfterAllNeutralGates()
    {
        TrapRequest request = CreateTrapRequest();
        NeutralTrapResult trap = NeutralTrapResult.Trap(
            request,
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        var routeRequest = new TrapCompletionRouteRequest(
            trap,
            RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
            TrapCompletionRouteDescriptor.RuntimeOwnedPublication,
            DomainValidated: true,
            BackendExecutionAuthorized: true,
            Qualification: 0x1234,
            FaultAddress: 0x5678,
            FaultAux: 0x9ABC,
            CompletionEvidenceClass: EvidenceVisibilityClass.CompatibilityAlias,
            CompletionMigrationClass:
                TrapCompletionMigrationClass.RecomputedAfterRestore);

        TrapCompletionRouteResult route =
            TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult fence =
            TrapCompletionRouteService.Default.EvaluateFence(
                routeRequest,
                route);

        Assert.Equal(TrapCompletionRouteDecision.Allowed, route.Decision);
        Assert.True(route.IsAllowed);
        Assert.True(route.CompletionPublicationAuthorized);
        Assert.True(route.RetirePublicationAuthorized);
        Assert.Equal((uint)NeutralTrapResultKind.CompatibilityOperationIntercept, route.NeutralReasonCode);

        Assert.Equal(TrapCompletionPublicationDecision.Allowed, fence.Decision);
        Assert.True(fence.CompletionPublicationAllowed);
        Assert.True(fence.RetirePublicationAllowed);
        Assert.Equal(CompletionRecordClass.Trap, fence.Completion.RecordClass);
        Assert.Equal(route.NeutralReasonCode, fence.Completion.ReasonCode);
        Assert.Equal(0x1234UL, fence.Completion.Qualification);
        Assert.Equal(0x5678UL, fence.Completion.FaultAddress);
        Assert.Equal(0x9ABCUL, fence.Completion.FaultAux);
    }

    [Fact]
    public void RuntimeOwnedCompletionPublicationRoute_SeparatesCompletionFromRetire()
    {
        TrapRequest request = CreateTrapRequest();
        NeutralTrapResult trap = NeutralTrapResult.Trap(
            request,
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        var routeRequest = new TrapCompletionRouteRequest(
            trap,
            RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
            TrapCompletionRouteDescriptor.RuntimeOwnedCompletionPublication,
            DomainValidated: true,
            BackendExecutionAuthorized: true,
            Qualification: 0x1234,
            FaultAddress: 0x5678,
            FaultAux: 0x9ABC);

        TrapCompletionRouteResult route =
            TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult fence =
            TrapCompletionRouteService.Default.EvaluateFence(
                routeRequest,
                route);

        Assert.Equal(TrapCompletionRouteDecision.DeniedRetirePublication, route.Decision);
        Assert.True(route.CompletionPublicationAuthorized);
        Assert.False(route.RetirePublicationAuthorized);
        Assert.True(route.CompletionPublicationAuthorizedOnly);
        Assert.False(route.IsFullyRetirable);
        Assert.False(route.IsAllowed);
        Assert.Equal((uint)NeutralTrapResultKind.CompatibilityOperationIntercept, route.NeutralReasonCode);
        Assert.Contains("authorizes completion publication", route.Reason);
        Assert.Contains("denies retire publication", route.Reason);

        Assert.Equal(
            TrapCompletionPublicationDecision.CompletionPublishedRetireDenied,
            fence.Decision);
        Assert.True(fence.CompletionPublicationAllowed);
        Assert.False(fence.RetirePublicationAllowed);
        Assert.True(fence.IsCompletionOnly);
        Assert.True(fence.IsRetireDenied);
        Assert.False(fence.IsDenied);
        Assert.Equal(CompletionRecordClass.Trap, fence.Completion.RecordClass);
        Assert.Equal(route.NeutralReasonCode, fence.Completion.ReasonCode);
        Assert.Equal(0x1234UL, fence.Completion.Qualification);
        Assert.Equal(0x5678UL, fence.Completion.FaultAddress);
        Assert.Equal(0x9ABCUL, fence.Completion.FaultAux);
    }

    [Fact]
    public void TrapCompletionRoute_RejectsCompatibilityProjectionOwnedRouteAuthority()
    {
        TrapRequest request = CreateTrapRequest();
        NeutralTrapResult trap = NeutralTrapResult.Trap(
            request,
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        var routeRequest = new TrapCompletionRouteRequest(
            trap,
            RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
            new TrapCompletionRouteDescriptor(
                CompletionRouteAuthority.CompatibilityProjection,
                allowsCompletionPublication: true,
                allowsRetirePublication: true,
                requiresValidatedDomain: true),
            DomainValidated: true,
            BackendExecutionAuthorized: true);

        TrapCompletionRouteResult route =
            TrapCompletionRouteService.Default.Authorize(routeRequest);

        Assert.Equal(TrapCompletionRouteDecision.DeniedRouteAuthority, route.Decision);
        Assert.False(route.CompletionPublicationAuthorized);
        Assert.False(route.RetirePublicationAuthorized);
        Assert.Contains("Compatibility projection cannot own trap completion routing", route.Reason);
    }

    [Fact]
    public void VmCallTrapAdmission_UsesNeutralCompletionRouteButStillDeniesPublication()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityTrapAdmissionResult result =
            service.AdmitVmCallTrapProjection(CreateVmCallRequest());

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
            result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.Equal(
            HypercallBackendAdmissionDecision.MissingBackendDescriptor,
            result.BackendAdmission.Decision);
        Assert.True(result.BackendAdmission.DeniesBackendExecution);
        Assert.Equal(
            TrapCompletionRouteDecision.DeniedBackendExecution,
            result.CompletionRoute.Decision);
        Assert.True(result.CompletionRoute.DeniesBackendExecution);
        Assert.Equal(
            TrapCompletionPublicationDecision.DeniedBackendExecution,
            result.PublicationFence.Decision);
        Assert.False(result.PublicationFence.CompletionPublicationAllowed);
        Assert.False(result.PublicationFence.RetirePublicationAllowed);
    }

    private static TrapRequest CreateTrapRequest() =>
        TrapRequest.ForVmxOperation(
            VmxOperationKind.VmCall,
            IsaOpcodeValues.VMCALL,
            vtId: 1,
            executionDomainTag: 2,
            addressSpaceTag: 3);

    private static VmxCompatibilityVmCallTrapAdmissionRequest CreateVmCallRequest() =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: false,
                allowMigrationSerializableState: false),
            TrapPolicy: new TrapPolicyDescriptor().WithEnabledClasses(
                TrapPolicyClass.CompatibilityOperation),
            TrapBitmap: CreateVmCallTrapBitmap(),
            VtId: 1,
            HypercallLeafRegister: 2,
            DescriptorRegister: 3,
            ExecutionDomainTag: 4,
            AddressSpaceTag: 5,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: true,
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
}
