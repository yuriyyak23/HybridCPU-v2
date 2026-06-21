using System;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core.Nested;

public static partial class NestedDomainController
{
    public static bool TryEnable(
        VmcsV2Descriptor descriptor,
        NestedEnablementRequest request,
        out VmcsV2ValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        bool enabled = TryEnable(
            CreateCompatibilityProjectionDomain(request),
            new ShadowVmcsNestedProjectionService(descriptor),
            request,
            out NestedValidationResult nestedValidation);

        validation = ToVmcsValidation(nestedValidation);
        return enabled;
    }

    public static void Disable(VmcsV2Descriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Disable(
            CreateCompatibilityProjectionDomain(),
            new ShadowVmcsNestedProjectionService(descriptor));
    }

    private static NestedDomainDescriptor CreateCompatibilityProjectionDomain() =>
        new(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            NestedCapabilityGrantMask.RequiredForPhase7,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true);

    private static NestedDomainDescriptor CreateCompatibilityProjectionDomain(
        NestedEnablementRequest request) =>
        new(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            request.NestedCapabilities,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: (request.ProvenGates & NestedEnablementGate.HostEvidenceExcluded) != 0,
            lanePassthroughBlocked: (request.ProvenGates & NestedEnablementGate.Lane6Lane7PassthroughBlocked) != 0);

    private static VmcsV2ValidationResult ToVmcsValidation(
        NestedValidationResult validation) =>
        validation.Succeeded
            ? VmcsV2ValidationResult.Success(VmcsV2BlockDirectory.ShadowVmcsBlockFieldId)
            : VmcsV2ValidationResult.Fail(
                validation.Code == NestedValidationCode.ProjectionDenied
                    ? VmcsV2ValidationCode.NestedPolicyGateFailed
                    : VmcsV2ValidationCode.InvalidVmcs12,
                VmcsV2BlockDirectory.ShadowVmcsBlockFieldId,
                validation.Message);
}
