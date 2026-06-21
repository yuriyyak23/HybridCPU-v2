using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmx;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class VmxTrapCompletionRouteRetirePublicationHardeningTests
{
    [Fact]
    public void VmCallProjectionOnlyRoute_DeniesBackendCompletionAndRetirePublication()
    {
        var service = new VmxCompatibilityAdmissionService();

        VmxCompatibilityTrapAdmissionResult result =
            service.AdmitVmCallTrapProjection(CreateVmCallRequest());

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
            result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.NeutralResult.ShouldTrap);
        Assert.Equal(VmExitReason.VmCall, result.ProjectedDecision.ExitReason);
        Assert.True(result.BackendAdmission.DeniesBackendExecution);

        Assert.Equal(
            TrapCompletionRouteDecision.DeniedBackendExecution,
            result.CompletionRoute.Decision);
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

        Assert.False(CompletionRecord.TryFromCompatibilityExit(
            result.PublicationFence,
            result.ProjectedDecision.ExitReason,
            result.ProjectedDecision.ExitQualification,
            out CompletionRecord record));
        Assert.True(record.IsEmpty);

        Assert.Throws<InvalidOperationException>(() =>
            CompletionRecord.FromCompatibilityExit(
                result.PublicationFence,
                result.ProjectedDecision.ExitReason,
                result.ProjectedDecision.ExitQualification));

        VmxRetireEffect effect = VmxRetireEffect.InterceptExit(
            result.ProjectedDecision,
            result.PublicationFence);
        Assert.True(effect.IsFaulted);
        Assert.False(effect.ExitGuestContextOnRetire);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);
    }

    [Fact]
    public void RuntimeOwnedPublicationDescriptor_DoesNotBypassBackendExecutionGate()
    {
        NeutralTrapResult trap = CreateNeutralVmCallTrap();
        TrapDecision projected = VmxTrapProjectionMapper.Default.Project(trap);
        var routeRequest = new TrapCompletionRouteRequest(
            trap,
            RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
            TrapCompletionRouteDescriptor.RuntimeOwnedPublication,
            DomainValidated: true,
            BackendExecutionAuthorized: false,
            Qualification: 0x1234,
            FaultAddress: 0x5678,
            FaultAux: 0x9ABC);

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
        Assert.False(CompletionRecord.TryFromCompatibilityExit(
            fence,
            projected.ExitReason,
            projected.ExitQualification,
            out CompletionRecord completion));
        Assert.True(completion.IsEmpty);
    }

    [Fact]
    public void PublicationFence_KeepsCompletionAndRetirePublicationAsSeparateDeniedGates()
    {
        NeutralTrapResult trap = CreateNeutralVmCallTrap();

        TrapCompletionPublicationFenceResult deniedRuntime =
            TrapCompletionPublicationFence.Default.Evaluate(
                trap,
                runtimeAdmissionAllowed: false,
                completionPublicationAuthorized: true,
                retirePublicationAuthorized: true,
                neutralReasonCode: (uint)trap.Kind);
        Assert.Equal(TrapCompletionPublicationDecision.DeniedRuntimeAdmission, deniedRuntime.Decision);
        Assert.False(deniedRuntime.CompletionPublicationAllowed);
        Assert.False(deniedRuntime.RetirePublicationAllowed);

        TrapCompletionPublicationFenceResult deniedCompletion =
            TrapCompletionPublicationFence.Default.Evaluate(
                trap,
                runtimeAdmissionAllowed: true,
                completionPublicationAuthorized: false,
                retirePublicationAuthorized: true,
                neutralReasonCode: (uint)trap.Kind);
        Assert.Equal(TrapCompletionPublicationDecision.DeniedBackendExecution, deniedCompletion.Decision);
        Assert.False(deniedCompletion.CompletionPublicationAllowed);
        Assert.False(deniedCompletion.RetirePublicationAllowed);
        Assert.True(deniedCompletion.Completion.IsEmpty);

        TrapCompletionPublicationFenceResult completionOnly =
            TrapCompletionPublicationFence.Default.Evaluate(
                trap,
                runtimeAdmissionAllowed: true,
                completionPublicationAuthorized: true,
                retirePublicationAuthorized: false,
                neutralReasonCode: (uint)trap.Kind,
                qualification: 0x1234,
                faultAddress: 0x5678,
                faultAux: 0x9ABC);
        Assert.Equal(
            TrapCompletionPublicationDecision.CompletionPublishedRetireDenied,
            completionOnly.Decision);
        Assert.True(completionOnly.CompletionPublicationAllowed);
        Assert.False(completionOnly.RetirePublicationAllowed);
        Assert.True(completionOnly.IsCompletionOnly);
        Assert.Equal(CompletionRecordClass.Trap, completionOnly.Completion.RecordClass);
        Assert.Equal(0x1234UL, completionOnly.Completion.Qualification);
        Assert.Equal(0x5678UL, completionOnly.Completion.FaultAddress);
        Assert.Equal(0x9ABCUL, completionOnly.Completion.FaultAux);
    }

    [Theory]
    [InlineData(
        EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
        TrapCompletionMigrationClass.RecomputedAfterRestore)]
    [InlineData(
        EvidenceVisibilityClass.CompatibilityAlias,
        TrapCompletionMigrationClass.Unclassified)]
    [InlineData(
        EvidenceVisibilityClass.CompatibilityAlias,
        TrapCompletionMigrationClass.HostOwnedNonMigratable)]
    public void PublicationFence_PublishesCompletionButDeniesRetireForUnsafeEvidenceOrMigration(
        EvidenceVisibilityClass evidenceClass,
        TrapCompletionMigrationClass migrationClass)
    {
        NeutralTrapResult trap = CreateNeutralVmCallTrap();

        TrapCompletionPublicationFenceResult result =
            TrapCompletionPublicationFence.Default.Evaluate(
                trap,
                runtimeAdmissionAllowed: true,
                completionPublicationAuthorized: true,
                retirePublicationAuthorized: true,
                neutralReasonCode: (uint)trap.Kind,
                qualification: 0x1234,
                evidenceClass: evidenceClass,
                migrationClass: migrationClass);

        Assert.Equal(
            TrapCompletionPublicationDecision.CompletionPublishedRetireDenied,
            result.Decision);
        Assert.True(result.CompletionPublicationAllowed);
        Assert.False(result.RetirePublicationAllowed);
        Assert.True(result.IsCompletionOnly);
        Assert.True(result.IsRetireDenied);
        Assert.Equal(CompletionRecordClass.Trap, result.Completion.RecordClass);
    }

    [Fact]
    public void RuntimeOwnedPublicationRoute_DefaultClassificationCannotImplicitlyGrantRetire()
    {
        NeutralTrapResult trap = CreateNeutralVmCallTrap();
        var routeRequest = new TrapCompletionRouteRequest(
            trap,
            RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed),
            TrapCompletionRouteDescriptor.RuntimeOwnedPublication,
            DomainValidated: true,
            BackendExecutionAuthorized: true,
            Qualification: 0x1234);

        TrapCompletionRouteResult route =
            TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult fence =
            TrapCompletionRouteService.Default.EvaluateFence(routeRequest, route);

        Assert.True(route.IsFullyRetirable);
        Assert.Equal(
            TrapCompletionPublicationDecision.CompletionPublishedRetireDenied,
            fence.Decision);
        Assert.True(fence.CompletionPublicationAllowed);
        Assert.False(fence.RetirePublicationAllowed);
        Assert.True(fence.IsCompletionOnly);
        Assert.Equal(CompletionRecordClass.Trap, fence.Completion.RecordClass);
    }

    [Fact]
    public void CompletionRecordProjection_RequiresFencePublishedCompatibilityExitRecord()
    {
        var projection = new CompletionProjectionService();
        CompletionRecord neutralTrapRecord = new(
            CompletionRecordClass.Trap,
            reasonCode: (uint)VmExitReason.VmCall,
            qualification: 0x1234,
            faultAddress: 0x5678,
            faultAux: 0x9ABC);

        Assert.False(neutralTrapRecord.IsCompatibilityProjectionSource);
        Assert.False(projection.CanProjectToVmx(neutralTrapRecord));
        Assert.Equal(VmExitReason.None, projection.ProjectReason(neutralTrapRecord));
        Assert.Equal(VmxCompletionProjection.None, projection.ProjectToVmx(neutralTrapRecord));

        TrapCompletionPublicationFenceResult deniedFence =
            TrapCompletionPublicationFence.Default.DenyProjectionOnly(
                CreateNeutralVmCallTrap(),
                runtimeAdmissionAllowed: true);

        Assert.False(CompletionRecord.TryFromCompatibilityExit(
            deniedFence,
            VmExitReason.VmCall,
            default,
            out CompletionRecord deniedCompatibilityRecord));
        Assert.True(deniedCompatibilityRecord.IsEmpty);
        Assert.False(projection.CanProjectToVmx(deniedCompatibilityRecord));
    }

    [Fact]
    public void RouteAndRetirePublicationSource_DoesNotIntroduceVmCallPublicationShortcuts()
    {
        string routeSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs");
        string fenceSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs");
        string compatibilityCompletionSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs");
        string vmcallFrontendSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs");
        string productionCallers = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs",
            "CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");

        Assert.Contains("TrapCompletionRouteDescriptor.ProjectionOnlyDenied", routeSource);
        Assert.Contains(
            "public static TrapCompletionRouteDescriptor RuntimeOwnedCompletionPublication",
            routeSource);
        Assert.Contains("RuntimeOwnedPublication", routeSource);
        Assert.Contains("DeniedBackendExecution", routeSource);
        Assert.Contains("DeniedCompletionPublication", routeSource);
        Assert.Contains("DeniedRetirePublication", routeSource);
        Assert.DoesNotContain("VmExitReason", routeSource);
        Assert.DoesNotContain("TrapDecision", routeSource);

        Assert.Contains("TrapCompletionPublicationFence", fenceSource);
        Assert.Contains("CompletionRecordClass.Trap", fenceSource);
        Assert.Contains("CompletionPublishedRetireDenied", fenceSource);
        Assert.Contains("TrapCompletionMigrationClass", fenceSource);
        Assert.Contains("CanPublishRetireEvidence", fenceSource);
        Assert.DoesNotContain("VmExitReason", fenceSource);
        Assert.DoesNotContain("TrapDecision", fenceSource);

        Assert.Contains("CompletionPublicationAllowed", compatibilityCompletionSource);
        Assert.Contains("CompletionRecordClass.CompatibilityExit", compatibilityCompletionSource);

        Assert.Contains("TrapCompletionRouteRequest.ProjectionOnlyDenied", vmcallFrontendSource);
        Assert.Contains("TrapCompletionRouteService.Default.EvaluateFence", vmcallFrontendSource);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedCompletionPublication", vmcallFrontendSource);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", vmcallFrontendSource);
        Assert.DoesNotContain("CompletionRecord.TryFromCompatibilityExit", vmcallFrontendSource);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", vmcallFrontendSource);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", vmcallFrontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", vmcallFrontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmFunc", vmcallFrontendSource);

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

    private static EvidencePolicyDescriptor CreateAliasEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);
}
