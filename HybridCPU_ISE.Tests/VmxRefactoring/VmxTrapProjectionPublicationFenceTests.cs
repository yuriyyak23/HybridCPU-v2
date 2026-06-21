using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmx;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using Xunit;

namespace HybridCPU_ISE.Tests;

public sealed class VmxTrapProjectionPublicationFenceTests
{
    [Fact]
    public void VmCallTrapProjection_DeniesCompletionAndRetirePublicationAfterAdmission()
    {
        var service = new VmxCompatibilityAdmissionService();
        VmxCompatibilityTrapAdmissionResult result = service.AdmitVmCallTrapProjection(
            CreateVmCallRequest());

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
            result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.NeutralResult.ShouldTrap);
        Assert.Equal(VmExitReason.VmCall, result.ProjectedDecision.ExitReason);
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
        Assert.True(result.PublicationFence.DeniesBackendExecution);
        Assert.False(result.PublicationFence.CompletionPublicationAllowed);
        Assert.False(result.PublicationFence.RetirePublicationAllowed);

        Assert.False(CompletionRecord.TryFromCompatibilityExit(
            result.PublicationFence,
            result.ProjectedDecision.ExitReason,
            result.ProjectedDecision.ExitQualification,
            out CompletionRecord completionRecord));
        Assert.True(completionRecord.IsEmpty);

        VmxRetireEffect effect = VmxRetireEffect.InterceptExit(
            result.ProjectedDecision,
            result.PublicationFence);
        Assert.True(effect.IsFaulted);
        Assert.Equal(VmxOperationKind.InterceptExit, effect.Operation);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);
    }

    [Fact]
    public void CompletionProjection_DoesNotTreatNeutralTrapReasonCodeAsVmxExitAuthority()
    {
        var neutralTrapRecord = new CompletionRecord(
            CompletionRecordClass.Trap,
            reasonCode: (uint)VmExitReason.VmCall,
            qualification: 0x1234,
            faultAddress: 0,
            faultAux: 0);
        var projection = new CompletionProjectionService();

        Assert.False(neutralTrapRecord.IsCompatibilityProjectionSource);
        Assert.False(projection.CanProjectToVmx(neutralTrapRecord));
        Assert.Equal(VmExitReason.None, projection.ProjectReason(neutralTrapRecord));
        Assert.Equal(VmExitReason.None, projection.ProjectToVmx(neutralTrapRecord).ExitReason);
    }

    [Fact]
    public void CompatibilityExitCompletion_RequiresNeutralPublicationFencePermit()
    {
        TrapRequest request = TrapRequest.ForVmxOperation(
            VmxOperationKind.VmCall,
            IsaOpcodeValues.VMCALL,
            vtId: 1,
            executionDomainTag: 2,
            addressSpaceTag: 3);
        NeutralTrapResult neutral = NeutralTrapResult.Trap(
            request,
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        TrapCompletionPublicationFenceResult permit = TrapCompletionPublicationFence.Default.Evaluate(
            neutral,
            runtimeAdmissionAllowed: true,
            completionPublicationAuthorized: true,
            retirePublicationAuthorized: true,
            neutralReasonCode: (uint)neutral.Kind,
            evidenceClass: EvidenceVisibilityClass.CompatibilityAlias,
            migrationClass: TrapCompletionMigrationClass.RecomputedAfterRestore);

        Assert.True(permit.CompletionPublicationAllowed);
        Assert.True(permit.RetirePublicationAllowed);
        Assert.True(CompletionRecord.TryFromCompatibilityExit(
            permit,
            VmExitReason.VmCall,
            TrapDecision.Exit(request, VmExitReason.VmCall).ExitQualification,
            out CompletionRecord compatibilityRecord));

        var projection = new CompletionProjectionService();
        Assert.True(compatibilityRecord.IsCompatibilityProjectionSource);
        Assert.True(projection.CanProjectToVmx(compatibilityRecord));
        Assert.Equal(VmExitReason.VmCall, projection.ProjectToVmx(compatibilityRecord).ExitReason);
    }

    [Fact]
    public void CompletionOnlyFence_PermitsCompletionProjectionButDeniesRetireEffect()
    {
        TrapRequest request = TrapRequest.ForVmxOperation(
            VmxOperationKind.VmCall,
            IsaOpcodeValues.VMCALL,
            vtId: 1,
            executionDomainTag: 2,
            addressSpaceTag: 3);
        NeutralTrapResult neutral = NeutralTrapResult.Trap(
            request,
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        TrapDecision projected = TrapDecision.Exit(request, VmExitReason.VmCall);

        TrapCompletionPublicationFenceResult completionOnly =
            TrapCompletionPublicationFence.Default.Evaluate(
                neutral,
                runtimeAdmissionAllowed: true,
                completionPublicationAuthorized: true,
                retirePublicationAuthorized: false,
                neutralReasonCode: (uint)neutral.Kind);

        Assert.True(completionOnly.CompletionPublicationAllowed);
        Assert.False(completionOnly.RetirePublicationAllowed);
        Assert.True(CompletionRecord.TryFromCompatibilityExit(
            completionOnly,
            projected.ExitReason,
            projected.ExitQualification,
            out CompletionRecord compatibilityRecord));
        Assert.True(compatibilityRecord.IsCompatibilityProjectionSource);

        VmxRetireEffect effect =
            VmxRetireEffect.InterceptExit(projected, completionOnly);
        Assert.True(effect.IsFaulted);
        Assert.False(effect.ExitGuestContextOnRetire);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);
    }

    [Fact]
    public void PublicationFence_SourceKeepsNeutralAuthorityAndNoProductionSuccessPath()
    {
        string neutralFenceSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs");
        string neutralRouteSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs");
        string neutralHypercallSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs");
        string compatibilitySource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Retire/VmxRetireModel.cs");
        string productionCallers = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs",
            "CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");

        Assert.Contains("TrapCompletionPublicationFence", neutralFenceSource);
        Assert.Contains("CompletionRecordClass.Trap", neutralFenceSource);
        Assert.DoesNotContain("VmExitReason", neutralFenceSource);
        Assert.DoesNotContain("Vmx", neutralFenceSource);
        Assert.DoesNotContain("VMX", neutralFenceSource);
        Assert.DoesNotContain("TrapDecision", neutralFenceSource);
        Assert.DoesNotContain("VmxExitQualification", neutralFenceSource);

        Assert.Contains("TrapCompletionRouteService", neutralRouteSource);
        Assert.Contains("TrapCompletionRouteDescriptor", neutralRouteSource);
        Assert.Contains("CompletionRouteAuthority.Runtime", neutralRouteSource);
        Assert.DoesNotContain("VmExitReason", neutralRouteSource);
        Assert.DoesNotContain("Vmx", neutralRouteSource);
        Assert.DoesNotContain("VMX", neutralRouteSource);
        Assert.DoesNotContain("TrapDecision", neutralRouteSource);
        Assert.DoesNotContain("VmxExitQualification", neutralRouteSource);

        Assert.Contains("HypercallBackendAdmissionService", neutralHypercallSource);
        Assert.Contains("CapabilityBoundaryRequirement", neutralHypercallSource);
        Assert.Contains("EvidenceBoundaryRequirement", neutralHypercallSource);
        Assert.Contains("HypercallBackendAuthority.Runtime", neutralHypercallSource);
        Assert.DoesNotContain("VmExitReason", neutralHypercallSource);
        Assert.DoesNotContain("Vmx", neutralHypercallSource);
        Assert.DoesNotContain("VMX", neutralHypercallSource);
        Assert.DoesNotContain("Vmcs", neutralHypercallSource);
        Assert.DoesNotContain("VMCS", neutralHypercallSource);
        Assert.DoesNotContain("TrapDecision", neutralHypercallSource);

        Assert.Contains("HypercallBackendAdmissionService.Default.Admit", compatibilitySource);
        Assert.Contains("HypercallBackendAdmissionRequest.MissingNeutralOwner", compatibilitySource);
        Assert.Contains("HypercallBackendAdmissionResult BackendAdmission", compatibilitySource);
        Assert.Contains("BackendAdmission.DeniesBackendExecution", compatibilitySource);
        Assert.Contains("TrapCompletionRouteRequest.ProjectionOnlyDenied", compatibilitySource);
        Assert.Contains("TrapCompletionRouteService.Default.Authorize", compatibilitySource);
        Assert.Contains("TrapCompletionRouteService.Default.EvaluateFence", compatibilitySource);
        Assert.Contains("TrapCompletionRouteResult CompletionRoute", compatibilitySource);
        Assert.Contains("TrapCompletionPublicationFenceResult publicationFence", compatibilitySource);
        Assert.Contains("publicationFence.CompletionPublicationAllowed", compatibilitySource);
        Assert.Contains("publicationFence.RetirePublicationAllowed", compatibilitySource);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", compatibilitySource);
        Assert.Contains("RecordClass == CompletionRecordClass.CompatibilityExit", compatibilitySource);
        Assert.Contains("VmxRetireEffect.Fault", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", productionCallers);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", productionCallers);
    }

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
