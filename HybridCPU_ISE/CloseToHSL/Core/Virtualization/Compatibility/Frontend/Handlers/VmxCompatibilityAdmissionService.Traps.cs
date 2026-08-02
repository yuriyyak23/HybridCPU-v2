using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCompatibilityTrapAdmissionDecision : byte
{
    TrapProjectionDeniedBackend = 0,
    DecodeDenied = 1,
    ProjectionDenied = 2,
    RuntimeAdmissionDenied = 3,
    TrapPolicyDenied = 4,
}

public readonly record struct VmxCompatibilityVmCallTrapAdmissionRequest(
    DomainRuntimeContext? Context,
    RootAuthorityDescriptor? RootAuthority,
    EvidencePolicyDescriptor? EvidencePolicy,
    TrapPolicyDescriptor? TrapPolicy,
    TrapPolicyBitmap? TrapBitmap,
    byte VtId,
    byte HypercallLeafRegister,
    byte DescriptorRegister,
    ushort ExecutionDomainTag,
    ushort AddressSpaceTag,
    bool DescriptorValidated,
    bool CapabilityValidated,
    bool SchedulingValidated,
    bool NoEmissionValidated,
    bool ProjectionEvidenceValidated,
    bool DomainValidated);

public readonly record struct VmxCompatibilityTrapAdmissionResult(
    VmxCompatibilityTrapAdmissionDecision Decision,
    VmxCompatDecodeResult Decode,
    VmxCompatProjectionResult Projection,
    RuntimeBoundaryAdmissionResult RuntimeAdmission,
    NeutralTrapResult NeutralResult,
    HypercallBackendAdmissionResult BackendAdmission,
    TrapDecision ProjectedDecision,
    TrapCompletionRouteResult CompletionRoute,
    TrapCompletionPublicationFenceResult PublicationFence,
    string Reason)
{
    public bool RuntimeAdmissionAllowed =>
        (Decision is not VmxCompatibilityTrapAdmissionDecision.DecodeDenied and
            not VmxCompatibilityTrapAdmissionDecision.ProjectionDenied) &&
        RuntimeAdmission.IsAllowed;

    public bool IsAdmittedDeniedTrapProjection =>
        Decision == VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend &&
        RuntimeAdmission.IsAllowed &&
        NeutralResult.ShouldTrap &&
        BackendAdmission.DeniesBackendExecution &&
        ProjectedDecision.ShouldExit &&
        CompletionRoute.DeniesBackendExecution &&
        PublicationFence.DeniesBackendExecution &&
        !PublicationFence.CompletionPublicationAllowed &&
        !PublicationFence.RetirePublicationAllowed;
}

public sealed partial class VmxCompatibilityAdmissionService
{
    public VmxCompatibilityTrapAdmissionResult AdmitVmCallTrapProjection(
        VmxCompatibilityVmCallTrapAdmissionRequest request)
    {
        TrapRequest trapRequest = TrapRequest.ForVmxOperation(
            VmxOperationKind.VmCall,
            IsaOpcodeValues.VMCALL,
            request.VtId,
            request.ExecutionDomainTag,
            request.AddressSpaceTag);

        VmxCompatDecodeResult decode = _decodeBoundary.Decode(new VmxCompatDecodeRequest(
            Opcode: IsaOpcodeValues.VMCALL,
            Rd: 0,
            Rs1: request.HypercallLeafRegister,
            Rs2: request.DescriptorRegister,
            DescriptorValidated: request.DescriptorValidated,
            CapabilityValidated: request.CapabilityValidated,
            SchedulingValidated: request.SchedulingValidated,
            NoEmissionValidated: request.NoEmissionValidated));

        if (!decode.IsAllowed)
        {
            return CreateTrapResult(
                VmxCompatibilityTrapAdmissionDecision.DecodeDenied,
                decode,
                default,
                default,
                NeutralTrapResult.Denied(trapRequest),
                decode.Reason);
        }

        VmxCompatProjectionResult projection = _projectionService.ValidateProjection(
            new VmxCompatProjectionRequest(
                CompatAliasSourceKind.Opcode,
                "VMCALL",
                DescriptorValidated: request.DescriptorValidated,
                EvidenceValidated: request.ProjectionEvidenceValidated,
                AttemptsAuthoritativeMutation: false));

        if (!projection.IsAllowed)
        {
            return CreateTrapResult(
                VmxCompatibilityTrapAdmissionDecision.ProjectionDenied,
                decode,
                projection,
                default,
                NeutralTrapResult.Denied(trapRequest),
                projection.Reason);
        }

        RuntimeBoundaryAdmissionResult admission = _runtimeAdmission.Validate(
            new RuntimeBoundaryAdmissionRequest(
                Context: request.Context,
                RootAuthority: request.RootAuthority,
                EvidencePolicy: request.EvidencePolicy,
                Operation: DomainRuntimeOperation.FromCompatibilityFrontend(
                    DomainRuntimeOperationKind.ProjectCompatibilityTrap,
                    requiresCapabilityGrant: false,
                    isProjectionOnly: true),
                DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
                CapabilityRequirement: CapabilityBoundaryRequirement.None,
                EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                    EvidenceVisibilityClass.CompatibilityAlias)));

        if (!admission.IsAllowed)
        {
            return CreateTrapResult(
                VmxCompatibilityTrapAdmissionDecision.RuntimeAdmissionDenied,
                decode,
                projection,
                admission,
                NeutralTrapResult.Denied(trapRequest),
                admission.Message);
        }

        if (!TryEvaluateNeutralTrapPolicy(
                request.TrapPolicy,
                request.TrapBitmap,
                trapRequest,
                request.DomainValidated,
                out NeutralTrapResult neutralResult,
                out string reason))
        {
            return CreateTrapResult(
                VmxCompatibilityTrapAdmissionDecision.TrapPolicyDenied,
                decode,
                projection,
                admission,
                neutralResult,
                reason);
        }

        return CreateTrapResult(
            VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
            decode,
            projection,
            admission,
            neutralResult,
            CreateHypercallBackendAdmission(request, admission, neutralResult));
    }

    private static bool TryEvaluateNeutralTrapPolicy(
        TrapPolicyDescriptor? descriptor,
        TrapPolicyBitmap? bitmap,
        TrapRequest request,
        bool domainValidated,
        out NeutralTrapResult result,
        out string reason)
    {
        if (descriptor is null)
        {
            result = NeutralTrapResult.Denied(request);
            reason = "VMCALL trap projection requires a runtime-owned trap policy descriptor.";
            return false;
        }

        if (!descriptor.IsRuntimeAuthoritative)
        {
            result = NeutralTrapResult.Denied(request);
            reason = "Compatibility projection cannot own VMCALL trap policy evaluation.";
            return false;
        }

        if (descriptor.RequiresValidatedDomain && !domainValidated)
        {
            result = NeutralTrapResult.Denied(request);
            reason = "VMCALL trap projection requires a validated domain descriptor.";
            return false;
        }

        if (!descriptor.AllowsClass(TrapPolicyClass.CompatibilityOperation))
        {
            result = NeutralTrapResult.Denied(request);
            reason = "Runtime trap policy descriptor denies compatibility-operation traps.";
            return false;
        }

        result = bitmap is null
            ? NeutralTrapResult.Continue(request)
            : bitmap.Evaluate(request);

        if (!result.ShouldTrap)
        {
            result = NeutralTrapResult.Denied(request);
            reason = "VMCALL trap projection requires a neutral compatibility-operation intercept.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static VmxCompatibilityTrapAdmissionResult CreateTrapResult(
        VmxCompatibilityTrapAdmissionDecision decision,
        VmxCompatDecodeResult decode,
        VmxCompatProjectionResult projection,
        RuntimeBoundaryAdmissionResult admission,
        NeutralTrapResult neutralResult,
        string reason)
    {
        return CreateTrapResult(
            decision,
            decode,
            projection,
            admission,
            neutralResult,
            HypercallBackendAdmissionResult.NotEvaluated(
                "Hypercall backend admission was not evaluated before this deny point."),
            reason);
    }

    private static VmxCompatibilityTrapAdmissionResult CreateTrapResult(
        VmxCompatibilityTrapAdmissionDecision decision,
        VmxCompatDecodeResult decode,
        VmxCompatProjectionResult projection,
        RuntimeBoundaryAdmissionResult admission,
        NeutralTrapResult neutralResult,
        HypercallBackendAdmissionResult backendAdmission)
    {
        return CreateTrapResult(
            decision,
            decode,
            projection,
            admission,
            neutralResult,
            backendAdmission,
            $"VMCALL trap projection admitted; {backendAdmission.Reason}");
    }

    private static VmxCompatibilityTrapAdmissionResult CreateTrapResult(
        VmxCompatibilityTrapAdmissionDecision decision,
        VmxCompatDecodeResult decode,
        VmxCompatProjectionResult projection,
        RuntimeBoundaryAdmissionResult admission,
        NeutralTrapResult neutralResult,
        HypercallBackendAdmissionResult backendAdmission,
        string reason)
    {
        TrapCompletionRouteRequest routeRequest =
            TrapCompletionRouteRequest.ProjectionOnlyDenied(
                neutralResult,
                admission,
                backendAdmission.IsAllowed);
        TrapCompletionRouteResult completionRoute =
            TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult publicationFence =
            TrapCompletionRouteService.Default.EvaluateFence(
                routeRequest,
                completionRoute);

        return new(
            decision,
            decode,
            projection,
            admission,
            neutralResult,
            backendAdmission,
            VmxTrapProjectionMapper.Default.Project(neutralResult),
            completionRoute,
            publicationFence,
            reason);
    }

    private static HypercallBackendAdmissionResult CreateHypercallBackendAdmission(
        VmxCompatibilityVmCallTrapAdmissionRequest request,
        RuntimeBoundaryAdmissionResult admission,
        NeutralTrapResult neutralResult) =>
        HypercallBackendAdmissionService.Default.Admit(
            HypercallBackendAdmissionRequest.MissingNeutralOwner(
                neutralResult,
                admission,
                request.Context?.Capabilities,
                request.EvidencePolicy,
                request.DomainValidated));
}
