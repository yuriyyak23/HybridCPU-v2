using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmx;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxHypercallBackendOwnerDecisionReadinessTests
{
    private const ulong HypercallBackendCapability = 1UL << 41;

    [Fact]
    public void VmCallAdmission_RemainsAdmittedDeniedWithoutBackendRouteOrPublication()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityTrapAdmissionResult result =
            service.AdmitVmCallTrapProjection(CreateVmCallRequest());

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
            result.Decision);
        Assert.True(result.Decode.IsAllowed);
        Assert.Equal(VmxOperandForm.HypercallLeafAndDescriptor, result.Decode.Payload.OperandForm);
        Assert.True(result.Projection.IsAllowed);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.NeutralResult.ShouldTrap);
        Assert.Equal(NeutralTrapResultKind.CompatibilityOperationIntercept, result.NeutralResult.Kind);
        Assert.Equal(VmExitReason.VmCall, result.ProjectedDecision.ExitReason);

        Assert.Equal(
            HypercallBackendAdmissionDecision.MissingBackendDescriptor,
            result.BackendAdmission.Decision);
        Assert.False(result.BackendAdmission.IsAllowed);
        Assert.True(result.BackendAdmission.DeniesBackendExecution);
        Assert.False(result.BackendAdmission.BackendExecutionAuthorized);

        Assert.Equal(
            TrapCompletionRouteDecision.DeniedBackendExecution,
            result.CompletionRoute.Decision);
        Assert.False(result.CompletionRoute.IsAllowed);
        Assert.True(result.CompletionRoute.DeniesBackendExecution);
        Assert.False(result.CompletionRoute.CompletionPublicationAuthorized);
        Assert.False(result.CompletionRoute.RetirePublicationAuthorized);

        Assert.Equal(
            TrapCompletionPublicationDecision.DeniedBackendExecution,
            result.PublicationFence.Decision);
        Assert.True(result.PublicationFence.DeniesBackendExecution);
        Assert.False(result.PublicationFence.CompletionPublicationAllowed);
        Assert.False(result.PublicationFence.RetirePublicationAllowed);
        Assert.True(result.PublicationFence.Completion.IsEmpty);
        Assert.True(result.IsAdmittedDeniedTrapProjection);
        Assert.Contains("backend execution remains denied", result.Reason);

        Assert.False(CompletionRecord.TryFromCompatibilityExit(
            result.PublicationFence,
            result.ProjectedDecision.ExitReason,
            result.ProjectedDecision.ExitQualification,
            out CompletionRecord completion));
        Assert.True(completion.IsEmpty);

        VmxRetireEffect retireEffect = VmxRetireEffect.InterceptExit(
            result.ProjectedDecision,
            result.PublicationFence);
        Assert.True(retireEffect.IsFaulted);
        Assert.Equal(VmxOperationKind.InterceptExit, retireEffect.Operation);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, retireEffect.FailureReason);
    }

    [Fact]
    public void BackendAdmission_LegacyMaterializedFlagWithoutOwnerDescriptorRemainsDenied()
    {
        HypercallBackendDescriptor descriptor = new(
            HypercallBackendAuthority.Runtime,
            CapabilityBoundaryRequirement.None,
            EvidenceBoundaryRequirement.None,
            requiresValidatedDomain: false,
            neutralBackendOwnerMaterialized: true);

        HypercallBackendAdmissionResult result =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                CreateNeutralVmCallTrap(),
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CapabilityDescriptorSet.Empty,
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerMissing,
            result.Decision);
        Assert.False(result.IsAllowed);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.True(result.DeniesBackendExecution);
        Assert.Contains("no neutral backend owner descriptor", result.Reason);
    }

    [Fact]
    public void BackendAdmission_HasNoPositivePathEvenWhenDraftOwnerSkeletonLooksComplete()
    {
        HypercallBackendDescriptor descriptor =
            HypercallBackendDescriptor.RuntimeOwnedDraftOwnerFence(
                CapabilityBoundaryRequirement.None,
                EvidenceBoundaryRequirement.None,
                NeutralHypercallBackendOwnerDescriptor.DraftNoStateCandidate(
                    ownerId: 0x060A));

        HypercallBackendAdmissionResult result =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                CreateNeutralVmCallTrap(),
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                descriptor,
                CapabilityDescriptorSet.Empty,
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));

        Assert.Equal(
            HypercallBackendAdmissionDecision.DeniedNeutralBackendOwnerRfcAdr,
            result.Decision);
        Assert.False(result.IsAllowed);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.True(result.DeniesBackendExecution);
        Assert.Contains("draft only", result.Reason);
        Assert.Contains("no accepted owner semantics", result.Reason);
    }

    [Fact]
    public void BackendAdmission_DeniesOwnerPreconditionsBeforeAnyPublicationRoute()
    {
        NeutralTrapResult neutralTrap = CreateNeutralVmCallTrap();
        HypercallBackendDescriptor runtimeDescriptor =
            HypercallBackendDescriptor.RuntimeOwnedDesignFence(
                CapabilityBoundaryRequirement.TypedGrant(
                    HypercallBackendCapability,
                    CapabilityGrantScope.DomainGranted),
                EvidenceBoundaryRequirement.GuestVisible(
                    EvidenceVisibilityClass.GuestArchitecturalState));

        HypercallBackendAdmissionResult deniedRuntime =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                neutralTrap,
                RuntimeBoundaryAdmissionResult.Denied(
                    RuntimeBoundaryAdmissionDecision.RuntimeAuthorityDenied,
                    "runtime authority denied",
                    DomainRuntimeAuthorityResult.Denied(
                        DomainRuntimeAuthorityDecision.MissingRootAuthority,
                        "missing root authority")),
                runtimeDescriptor,
                CreateCapabilities(HypercallBackendCapability),
                GuestArchitecturalEvidencePolicy(),
                DomainValidated: true));
        Assert.Equal(HypercallBackendAdmissionDecision.DeniedRuntimeAdmission, deniedRuntime.Decision);
        Assert.True(deniedRuntime.DeniesBackendExecution);

        HypercallBackendAdmissionResult deniedNoTrap =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                NeutralTrapResult.Continue(neutralTrap.Request),
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                runtimeDescriptor,
                CreateCapabilities(HypercallBackendCapability),
                GuestArchitecturalEvidencePolicy(),
                DomainValidated: true));
        Assert.Equal(HypercallBackendAdmissionDecision.DeniedNoNeutralTrap, deniedNoTrap.Decision);
        Assert.True(deniedNoTrap.DeniesBackendExecution);

        HypercallBackendAdmissionResult deniedAuthority =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                neutralTrap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                new HypercallBackendDescriptor(
                    HypercallBackendAuthority.CompatibilityProjection,
                    CapabilityBoundaryRequirement.None,
                    EvidenceBoundaryRequirement.None,
                    requiresValidatedDomain: false,
                    neutralBackendOwnerMaterialized: false),
                CapabilityDescriptorSet.Empty,
                EvidencePolicyDescriptor.FailClosed,
                DomainValidated: true));
        Assert.Equal(HypercallBackendAdmissionDecision.DeniedBackendAuthority, deniedAuthority.Decision);
        Assert.True(deniedAuthority.DeniesBackendExecution);

        HypercallBackendAdmissionResult deniedDomain =
            HypercallBackendAdmissionService.Default.Admit(new HypercallBackendAdmissionRequest(
                neutralTrap,
                RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
                runtimeDescriptor,
                CreateCapabilities(HypercallBackendCapability),
                GuestArchitecturalEvidencePolicy(),
                DomainValidated: false));
        Assert.Equal(HypercallBackendAdmissionDecision.DeniedDomainValidation, deniedDomain.Decision);
        Assert.True(deniedDomain.DeniesBackendExecution);
    }

    [Fact]
    public void VmCallExitReasonAndTrapProjectionDoNotUnlockRuntimeOwnedPublication()
    {
        NeutralTrapResult neutralTrap = CreateNeutralVmCallTrap();
        TrapDecision projected = VmxTrapProjectionMapper.Default.Project(neutralTrap);

        Assert.True(projected.ShouldExit);
        Assert.Equal(VmExitReason.VmCall, projected.ExitReason);

        var routeRequest = new TrapCompletionRouteRequest(
            neutralTrap,
            RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
            TrapCompletionRouteDescriptor.RuntimeOwnedPublication,
            DomainValidated: true,
            BackendExecutionAuthorized: false,
            Qualification: 0);

        TrapCompletionRouteResult route =
            TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult fence =
            TrapCompletionRouteService.Default.EvaluateFence(routeRequest, route);

        Assert.Equal(TrapCompletionRouteDecision.DeniedBackendExecution, route.Decision);
        Assert.True(route.DeniesBackendExecution);
        Assert.False(route.CompletionPublicationAuthorized);
        Assert.False(route.RetirePublicationAuthorized);
        Assert.Equal(TrapCompletionPublicationDecision.DeniedBackendExecution, fence.Decision);
        Assert.False(fence.CompletionPublicationAllowed);
        Assert.False(fence.RetirePublicationAllowed);
        Assert.True(fence.Completion.IsEmpty);
    }

    [Fact]
    public void VmCallOwnerDecisionSource_DoesNotIntroduceBackendOrPublicationShortcuts()
    {
        string hypercallSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs",
            "CloseToRTL/Core/Runtime/Events/Hypercalls/NeutralHypercallBackendOwnerDescriptor.cs");
        string vmcallFrontendSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs");
        string routeAndFenceSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs",
            "CloseToRTL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs");
        string productionCallers = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs",
            "CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");

        Assert.Contains("HypercallBackendAdmissionService", hypercallSource);
        Assert.Contains("DeniedNeutralBackendOwnerMissing", hypercallSource);
        Assert.Contains("DeniedNeutralBackendOwnerRfcAdr", hypercallSource);
        Assert.Contains("NeutralHypercallBackendOwnerDescriptor", hypercallSource);
        Assert.Contains("DraftOnly", hypercallSource);
        Assert.DoesNotContain("NeutralHypercallBackendOwnerRfcAdrState.Accepted", hypercallSource);
        Assert.DoesNotContain("Accepted = ", hypercallSource);
        Assert.Contains("return Deny(", hypercallSource);
        Assert.DoesNotContain("HypercallBackendAdmissionDecision.Allowed", hypercallSource);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", hypercallSource);
        Assert.DoesNotContain("AllowedBackend", hypercallSource);
        Assert.DoesNotContain("RuntimeOwnedPublication", hypercallSource);
        Assert.DoesNotContain("VmExitReason", hypercallSource);
        Assert.DoesNotContain("TrapDecision", hypercallSource);
        Assert.DoesNotContain("Vmx", hypercallSource);
        Assert.DoesNotContain("VMX", hypercallSource);

        Assert.Contains("HypercallBackendAdmissionRequest.MissingNeutralOwner", vmcallFrontendSource);
        Assert.Contains("TrapCompletionRouteRequest.ProjectionOnlyDenied", vmcallFrontendSource);
        Assert.Contains("backendAdmission.IsAllowed", vmcallFrontendSource);
        Assert.Contains("VmxTrapProjectionMapper.Default.Project", vmcallFrontendSource);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", vmcallFrontendSource);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", vmcallFrontendSource);
        Assert.DoesNotContain("new HypercallBackendDescriptor", vmcallFrontendSource);
        Assert.DoesNotContain("CompletionRecord.TryFromCompatibilityExit", vmcallFrontendSource);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", vmcallFrontendSource);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", vmcallFrontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", vmcallFrontendSource);

        Assert.Contains("RuntimeOwnedPublication", routeAndFenceSource);
        Assert.Contains("BackendExecutionAuthorized", routeAndFenceSource);
        Assert.Contains("DeniedBackendExecution", routeAndFenceSource);

        Assert.Contains("VmxRetireEffect.Fault", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.VmFunc", productionCallers);
        Assert.DoesNotContain("CompletionRecord.TryFromCompatibilityExit", productionCallers);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", productionCallers);
    }

    private static VmxCompatibilityVmCallTrapAdmissionRequest CreateVmCallRequest() =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: CreateAliasEvidencePolicy(),
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

    private static NeutralTrapResult CreateNeutralVmCallTrap() =>
        NeutralTrapResult.Trap(
            TrapRequest.ForVmxOperation(
                VmxOperationKind.VmCall,
                IsaOpcodeValues.VMCALL,
                vtId: 1,
                executionDomainTag: 2,
                addressSpaceTag: 3),
            NeutralTrapResultKind.CompatibilityOperationIntercept);

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

    private static CapabilityDescriptorSet CreateCapabilities(ulong capability) =>
        new(new CapabilityGrantCollection(
        [
            new CapabilityGrant(
                capability,
                CapabilityGrantScope.DomainGranted,
                isGranted: true),
        ]));

    private static EvidencePolicyDescriptor CreateAliasEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);

    private static EvidencePolicyDescriptor GuestArchitecturalEvidencePolicy() =>
        new(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);
}
